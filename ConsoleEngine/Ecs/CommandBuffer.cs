using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Records structural changes (Create, Destroy, Add, Remove) to apply later, when no query is being iterated.
/// A <see cref="SystemGroup"/> plays its buffer back after every system has run. Commands are stored as bytes in one
/// reused array, so recording doesn't allocate once the array has grown to the frame's needs.
/// </summary>
/// <remarks>
/// Playback skips commands for entities that are no longer alive, so destroying the same entity twice (two bullets
/// hitting one asteroid) is safe.
/// <para>
/// <c>Create</c> returns a <b>deferred</b> handle: an <see cref="Entity"/> with a negative id that only this buffer
/// understands, until its next playback or clear. Pass it to later <c>Add</c>/<c>Remove</c>/<c>Destroy</c> calls on
/// the same buffer; playback swaps it for the real entity. The world doesn't know it (<c>IsAlive</c> is false).
/// </para>
/// </remarks>
public sealed partial class CommandBuffer
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Process-wide, so a deferred handle from another buffer or an earlier playback is never accepted.</summary>
    private static int _lastGeneration;

    private byte[] _buffer;
    private int _length;

    /// <summary>Real entities created during the current playback, indexed by deferred handle (-Id - 1).</summary>
    private Entity[] _created = new Entity[16];
    private int _createCount;
    private int _playedCreateCount;
    private int _generation = NextGeneration();

    //**********************************************************
    //** ctor
    //**********************************************************

    public CommandBuffer(int initialCapacity = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        _buffer = new byte[initialCapacity];
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Number of commands waiting for playback.</summary>
    public int Count { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Destroy(Entity entity)
    {
        WriteValue(Command.Destroy);
        WriteValue(entity);
        Count++;
    }

    public void Add<T>(Entity entity, in T value = default) where T : unmanaged
    {
        WriteValue(Command.Add);
        WriteValue(entity);
        WriteComponent(value);
        Count++;
    }

    public void Remove<T>(Entity entity) where T : unmanaged
    {
        WriteValue(Command.Remove);
        WriteValue(entity);
        WriteValue(ComponentType<T>.Id);
        Count++;
    }

    /// <summary>Applies every command in recording order, then clears the buffer.</summary>
    public void Playback(World world)
    {
        var position = 0;
        try
        {
            while (position < _length)
            {
                var command = ReadValue<Command>(ref position);
                var entity = command == Command.Create ? Entity.Null : Resolve(ReadValue<Entity>(ref position));
                switch (command)
                {
                    case Command.Destroy:
                        if (world.IsAlive(entity))
                            world.Destroy(entity);
                        break;
                    case Command.Add:
                        PlaybackAdd(world, entity, ref position);
                        break;
                    case Command.Remove:
                        var componentId = ReadValue<int>(ref position);
                        if (world.IsAlive(entity))
                            world.RemoveComponent(entity, componentId);
                        break;
                    case Command.Create:
                        PlaybackCreate(world, ref position);
                        break;
                }
            }
        }
        finally
        {
            Clear();
        }
    }

    /// <summary>
    /// Drops all recorded commands without applying them. Deferred handles from this buffer become invalid.
    /// </summary>
    public void Clear()
    {
        _length = 0;
        Count = 0;
        _createCount = 0;
        _playedCreateCount = 0;
        _generation = NextGeneration();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void PlaybackAdd(World world, Entity entity, ref int position)
    {
        var componentId = ReadValue<int>(ref position);
        var data = ReadComponentData(componentId, ref position);
        if (!world.IsAlive(entity))
            return;

        var archetype = world.AddComponent(entity, componentId, out var chunkIndex, out var row);
        World.WriteRaw(archetype, ref archetype.GetChunk(chunkIndex), row, componentId, data);
    }

    /// <summary>Two passes over the recorded components: build the mask, then write each component's data.</summary>
    private void PlaybackCreate(World world, ref int position)
    {
        var componentCount = ReadValue<int>(ref position);
        var start = position;
        var mask = default(ComponentMask);
        for (var i = 0; i < componentCount; i++)
        {
            var componentId = ReadValue<int>(ref position);
            mask.Add(componentId);
            position += ComponentRegistry.SizeOf(componentId);
        }

        var archetype = world.CreateWithMask(mask, out var entity, out var chunkIndex, out var row);
        _created[_playedCreateCount++] = entity;
        ref var chunk = ref archetype.GetChunk(chunkIndex);
        position = start;
        for (var i = 0; i < componentCount; i++)
        {
            var componentId = ReadValue<int>(ref position);
            World.WriteRaw(archetype, ref chunk, row, componentId, ReadComponentData(componentId, ref position));
        }
    }

    /// <summary>
    /// Starts a Create command and returns its deferred handle. Checks the component list for duplicates, like
    /// <see cref="World"/> does.
    /// </summary>
    private Entity BeginCreate(ReadOnlySpan<int> componentIds)
    {
        var mask = default(ComponentMask);
        for (var i = 0; i < componentIds.Length; i++)
        {
            if (mask.Contains(componentIds[i]))
                EcsThrowHelper.ThrowDuplicateComponent(ComponentRegistry.TypeOf(componentIds[i]));
            mask.Add(componentIds[i]);
        }

        WriteValue(Command.Create);
        WriteValue(componentIds.Length);
        Count++;

        // Reserve the slot the real entity will land in at playback, so the array never grows during playback.
        if (_createCount == _created.Length)
            Array.Resize(ref _created, _created.Length * 2);
        return new Entity(-++_createCount, _generation);
    }

    /// <summary>Swaps a deferred handle for the entity its Create made. Real handles pass through unchanged.</summary>
    private Entity Resolve(Entity entity)
    {
        if (entity.Id >= 0)
            return entity;

        if (entity.Version != _generation)
            EcsThrowHelper.ThrowForeignDeferredEntity(entity);

        return _created[-entity.Id - 1];
    }

    private static int NextGeneration() => Interlocked.Increment(ref _lastGeneration);

    /// <summary>Writes the component id followed by its bytes (none for tags).</summary>
    private void WriteComponent<T>(in T value) where T : unmanaged
    {
        WriteValue(ComponentType<T>.Id);
        if (!ComponentType<T>.IsTag)
            WriteValue(value);
    }

    private void WriteValue<T>(in T value) where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        if (_length + size > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + size));

        Unsafe.WriteUnaligned(ref _buffer[_length], value);
        _length += size;
    }

    private T ReadValue<T>(ref int position) where T : unmanaged
    {
        var value = Unsafe.ReadUnaligned<T>(ref _buffer[position]);
        position += Unsafe.SizeOf<T>();
        return value;
    }

    private ReadOnlySpan<byte> ReadComponentData(int componentId, ref int position)
    {
        var size = ComponentRegistry.SizeOf(componentId);
        var data = new ReadOnlySpan<byte>(_buffer, position, size);
        position += size;
        return data;
    }

    private enum Command : byte
    {
        Create,
        Destroy,
        Add,
        Remove
    }
}
