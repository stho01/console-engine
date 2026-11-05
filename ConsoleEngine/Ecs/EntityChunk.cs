using System;

namespace ConsoleEngine.Ecs;

/// <summary>
/// One chunk of a query result: <see cref="Count"/> dense rows, one span per component. A <c>ref struct</c>, so it
/// can't outlive the loop. Don't make structural changes (Create, Add, Remove, Destroy) while holding one.
/// </summary>
public readonly unsafe ref struct EntityChunk
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Archetype _archetype;
    private readonly byte* _memory;

    //**********************************************************
    //** ctor
    //**********************************************************

    internal EntityChunk(Archetype archetype, in Chunk chunk)
    {
        _archetype = archetype;
        _memory = chunk.Memory;
        Count = chunk.Count;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Count { get; }

    public ReadOnlySpan<Entity> Entities => new((Entity*)_memory, Count);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The component column of this chunk. Index <c>i</c> belongs to <c>Entities[i]</c>.</summary>
    public Span<T> GetSpan<T>() where T : unmanaged
    {
        if (ComponentType<T>.IsTag)
            EcsThrowHelper.ThrowTagHasNoData(typeof(T));

        var offset = _archetype.GetOffset(ComponentType<T>.Id);
        if (offset < 0)
            EcsThrowHelper.ThrowNotInChunk(typeof(T));

        return new Span<T>(_memory + offset, Count);
    }

    /// <summary>Whether this chunk's entities have <typeparamref name="T"/>. Use for optional components.</summary>
    public bool Has<T>() where T : unmanaged => _archetype.Mask.Contains(ComponentType<T>.Id);
}
