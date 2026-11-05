using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Owns entities and their components. Component data lives in native memory (archetype chunks), so a world must
/// be disposed to free it. Not thread-safe: use it from the game-loop thread only.
/// </summary>
public sealed unsafe partial class World : IDisposable
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly ChunkPool _chunkPool = new();
    private readonly Dictionary<ComponentMask, Archetype> _archetypesByMask = new();
    private readonly Archetype _emptyArchetype;
    private Archetype[] _archetypes = new Archetype[16];
    private int _archetypeCount;

    /// <summary>Where each entity id lives. Indexed by <see cref="Entity.Id"/>.</summary>
    private EntityRecord[] _records;
    private int _recordCount;
    private int[] _freeIds;
    private int _freeCount;
    private int _iterationDepth;
    private bool _disposed;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="initialCapacity">Entity slots to reserve up front. The table grows by doubling.</param>
    public World(int initialCapacity = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        _records = new EntityRecord[initialCapacity];
        _freeIds = new int[initialCapacity];
        _emptyArchetype = GetOrCreateArchetype(default);
    }

    ~World() => FreeNativeMemory();

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Number of alive entities.</summary>
    public int Count { get; private set; }

    public int ArchetypeCount => _archetypeCount;

    internal ChunkPool ChunkPool => _chunkPool;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Creates an entity without components.</summary>
    public Entity Create()
    {
        ThrowIfStructuralChangeNotAllowed();
        return CreateIn(_emptyArchetype, out _, out _);
    }

    public bool IsAlive(Entity entity) =>
        (uint)entity.Id < (uint)_recordCount && _records[entity.Id].Version == entity.Version;

    public void Destroy(Entity entity)
    {
        ThrowIfStructuralChangeNotAllowed();
        ref var record = ref GetAliveRecord(entity);
        RemoveFromArchetype(ref record);

        record.Version = record.Version == int.MaxValue ? 1 : record.Version + 1;
        record.ArchetypeIndex = -1;
        _freeIds[_freeCount++] = entity.Id;
        Count--;
    }

    public bool Has<T>(Entity entity) where T : unmanaged =>
        _archetypes[GetAliveRecord(entity).ArchetypeIndex].Mask.Contains(ComponentType<T>.Id);

    /// <summary>
    /// Returns a reference to the component. It stays valid until the entity changes archetype (Add, Remove,
    /// Destroy of this or another entity in the same archetype).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>(Entity entity) where T : unmanaged
    {
        if (ComponentType<T>.IsTag)
            EcsThrowHelper.ThrowTagHasNoData(typeof(T));

        ref var record = ref GetAliveRecord(entity);
        var archetype = _archetypes[record.ArchetypeIndex];
        var offset = archetype.GetOffset(ComponentType<T>.Id);
        if (offset < 0)
            EcsThrowHelper.ThrowMissingComponent(entity, typeof(T));

        return ref ((T*)(archetype.GetChunk(record.ChunkIndex).Memory + offset))[record.Row];
    }

    /// <summary>Overwrites a component the entity already has.</summary>
    public void Set<T>(Entity entity, in T value) where T : unmanaged => Get<T>(entity) = value;

    /// <summary>Adds a component the entity doesn't have yet. Moves the entity to another archetype.</summary>
    public void Add<T>(Entity entity, in T value = default) where T : unmanaged
    {
        var target = AddComponent(entity, ComponentType<T>.Id, out var chunkIndex, out var row);
        Write(target, ref target.GetChunk(chunkIndex), row, value);
    }

    /// <summary>Removes a component the entity has. Moves the entity to another archetype.</summary>
    public void Remove<T>(Entity entity) where T : unmanaged => RemoveComponent(entity, ComponentType<T>.Id);

    /// <summary>Creates a query. Keep and reuse it: matching is cached inside the query.</summary>
    public Query Query(in QueryDescription description) => new(this, description);

    /// <summary>Frees all component memory. Every entity handle stops being alive.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        FreeNativeMemory();
        _disposed = true;
        _recordCount = 0;
        _freeCount = 0;
        Count = 0;
        GC.SuppressFinalize(this);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    internal Archetype GetArchetype(Entity entity) => _archetypes[GetAliveRecord(entity).ArchetypeIndex];

    internal Archetype GetArchetypeAt(int index) => _archetypes[index];

    /// <summary>Called when a query enumeration starts. Structural changes throw until the matching end.</summary>
    internal void BeginIteration() => _iterationDepth++;

    internal void EndIteration() => _iterationDepth--;

    /// <summary>Moves the entity to the archetype with one more component. The new component's data is unwritten.</summary>
    internal Archetype AddComponent(Entity entity, int componentId, out int chunkIndex, out int row)
    {
        ThrowIfStructuralChangeNotAllowed();
        ref var record = ref GetAliveRecord(entity);
        var source = _archetypes[record.ArchetypeIndex];
        if (source.Mask.Contains(componentId))
            EcsThrowHelper.ThrowAlreadyHasComponent(entity, ComponentRegistry.TypeOf(componentId));

        if (!source.TryGetAddEdge(componentId, out var target))
        {
            target = GetOrCreateArchetype(source.Mask.With(componentId));
            source.SetAddEdge(componentId, target);
            target.SetRemoveEdge(componentId, source);
        }

        MoveTo(entity, ref record, source, target);
        chunkIndex = record.ChunkIndex;
        row = record.Row;
        return target;
    }

    internal void RemoveComponent(Entity entity, int componentId)
    {
        ThrowIfStructuralChangeNotAllowed();
        ref var record = ref GetAliveRecord(entity);
        var source = _archetypes[record.ArchetypeIndex];
        if (!source.Mask.Contains(componentId))
            EcsThrowHelper.ThrowMissingComponent(entity, ComponentRegistry.TypeOf(componentId));

        if (!source.TryGetRemoveEdge(componentId, out var target))
        {
            target = GetOrCreateArchetype(source.Mask.Without(componentId));
            source.SetRemoveEdge(componentId, target);
            target.SetAddEdge(componentId, source);
        }

        MoveTo(entity, ref record, source, target);
    }

    /// <summary>Creates an entity in the archetype for <paramref name="mask"/>. Component data is unwritten.</summary>
    internal Archetype CreateWithMask(in ComponentMask mask, out Entity entity, out int chunkIndex, out int row)
    {
        ThrowIfStructuralChangeNotAllowed();
        var archetype = GetOrCreateArchetype(mask);
        entity = CreateIn(archetype, out chunkIndex, out row);
        return archetype;
    }

    /// <summary>Copies raw component bytes into a row. Used by <see cref="CommandBuffer"/> playback.</summary>
    internal static void WriteRaw(Archetype archetype, ref Chunk chunk, int row, int componentId, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        var destination = chunk.Memory + archetype.GetOffset(componentId) + row * data.Length;
        data.CopyTo(new Span<byte>(destination, data.Length));
    }

    private void ThrowIfStructuralChangeNotAllowed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_iterationDepth != 0)
            EcsThrowHelper.ThrowStructuralChangeDuringIteration();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref EntityRecord GetAliveRecord(Entity entity)
    {
        if ((uint)entity.Id >= (uint)_recordCount)
            EcsThrowHelper.ThrowNotAlive(entity);

        ref var record = ref _records[entity.Id];
        if (record.Version != entity.Version)
            EcsThrowHelper.ThrowNotAlive(entity);

        return ref record;
    }

    private Entity CreateIn(Archetype archetype, out int chunkIndex, out int row)
    {
        var entity = NewEntity();
        archetype.Allocate(entity, _chunkPool, out chunkIndex, out row);
        _records[entity.Id] = new EntityRecord(entity.Version, archetype.Index, chunkIndex, row);
        return entity;
    }

    /// <summary>Takes a free id (or a new one) without placing the entity in an archetype.</summary>
    private Entity NewEntity()
    {
        int id;
        if (_freeCount > 0)
        {
            id = _freeIds[--_freeCount];
        }
        else
        {
            if (_recordCount == _records.Length)
            {
                Array.Resize(ref _records, _records.Length * 2);
                Array.Resize(ref _freeIds, _records.Length);
            }

            id = _recordCount++;
            _records[id].Version = 1;
        }

        Count++;
        return new Entity(id, _records[id].Version);
    }

    private void MoveTo(Entity entity, ref EntityRecord record, Archetype source, Archetype target)
    {
        target.Allocate(entity, _chunkPool, out var chunkIndex, out var row);
        Archetype.CopyShared(
            source, ref source.GetChunk(record.ChunkIndex), record.Row, target, ref target.GetChunk(chunkIndex), row);

        RemoveFromArchetype(ref record);
        record.ArchetypeIndex = target.Index;
        record.ChunkIndex = chunkIndex;
        record.Row = row;
    }

    /// <summary>Removes the record's row and repoints the entity that swap-back moved into it.</summary>
    private void RemoveFromArchetype(ref EntityRecord record)
    {
        var moved = _archetypes[record.ArchetypeIndex].RemoveAt(record.ChunkIndex, record.Row, _chunkPool);
        if (moved == Entity.Null)
            return;

        ref var movedRecord = ref _records[moved.Id];
        movedRecord.ChunkIndex = record.ChunkIndex;
        movedRecord.Row = record.Row;
    }

    private Archetype GetOrCreateArchetype(in ComponentMask mask)
    {
        if (_archetypesByMask.TryGetValue(mask, out var archetype))
            return archetype;

        archetype = new Archetype(_archetypeCount, mask);
        if (_archetypeCount == _archetypes.Length)
            Array.Resize(ref _archetypes, _archetypes.Length * 2);

        _archetypes[_archetypeCount++] = archetype;
        _archetypesByMask.Add(mask, archetype);
        return archetype;
    }

    private static void Write<T>(Archetype archetype, ref Chunk chunk, int row, in T value) where T : unmanaged
    {
        if (ComponentType<T>.IsTag)
            return;

        ((T*)(chunk.Memory + archetype.GetOffset(ComponentType<T>.Id)))[row] = value;
    }

    private static void Fill<T>(Archetype archetype, ref Chunk chunk, int firstRow, int count, in T value)
        where T : unmanaged
    {
        if (ComponentType<T>.IsTag)
            return;

        new Span<T>((T*)(chunk.Memory + archetype.GetOffset(ComponentType<T>.Id)) + firstRow, count).Fill(value);
    }

    private void FreeNativeMemory()
    {
        for (var i = 0; i < _archetypeCount; i++)
            _archetypes[i].FreeChunks();

        _chunkPool.Dispose();
    }

    /// <summary>16 bytes, so four records share a cache line. Read together on every lookup.</summary>
    private struct EntityRecord(int version, int archetypeIndex, int chunkIndex, int row)
    {
        public int Version = version;
        public int ArchetypeIndex = archetypeIndex;
        public int ChunkIndex = chunkIndex;
        public int Row = row;
    }
}
