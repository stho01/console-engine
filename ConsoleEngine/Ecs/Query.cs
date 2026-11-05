using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConsoleEngine.Ecs;

/// <summary>
/// A cached set of archetypes matching a <see cref="QueryDescription"/>. Create it once (in a system's
/// <c>OnCreate</c>) and reuse it every frame: archetypes created later are picked up incrementally, and iterating
/// never allocates. Don't make structural changes while iterating. Describe it with the static entry points:
/// <c>World.Query(Query.With&lt;Position, Velocity&gt;().Without&lt;Frozen&gt;())</c>.
/// </summary>
public sealed class Query
{
    //**********************************************************
    //** description entry points
    //**********************************************************

    // Shorthands for `new QueryDescription().With...`. They live here because QueryDescription can't have static
    // methods with the same signatures as its instance methods.

    public static QueryDescription With<T>() where T : unmanaged => new QueryDescription().With<T>();

    public static QueryDescription With<T1, T2>() where T1 : unmanaged where T2 : unmanaged =>
        new QueryDescription().With<T1, T2>();

    public static QueryDescription With<T1, T2, T3>() where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged =>
        new QueryDescription().With<T1, T2, T3>();

    public static QueryDescription Without<T>() where T : unmanaged => new QueryDescription().Without<T>();

    //**********************************************************
    //** fields
    //**********************************************************

    private readonly World _world;
    private readonly ComponentMask _all;
    private readonly ComponentMask _none;
    private Archetype[] _archetypes = new Archetype[4];
    private int _archetypeCount;
    private int _checkedArchetypeCount;

    //**********************************************************
    //** ctor
    //**********************************************************

    internal Query(World world, in QueryDescription description)
    {
        _world = world;
        _all = description.All;
        _none = description.None;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Number of entities that match right now.</summary>
    public int Count
    {
        get
        {
            Refresh();
            var count = 0;
            for (var i = 0; i < _archetypeCount; i++)
                count += _archetypes[i].EntityCount;
            return count;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Chunk-by-chunk iteration: <c>foreach (var chunk in query.GetChunks())</c>. Fastest, SIMD-friendly. Until the
    /// loop ends, Create/Destroy/Add/Remove on the world throw: record them in a <see cref="CommandBuffer"/>.
    /// </summary>
    public ChunkEnumerable GetChunks()
    {
        Refresh();
        return new ChunkEnumerable(_world, _archetypes, _archetypeCount);
    }

    // The ForEach overloads run the job on a local copy and write it back at the end. Through `ref job` the JIT must
    // assume component writes can alias the job, so it would reload the job's fields for every entity.

    public void ForEach<TJob, T1>(ref TJob job)
        where TJob : struct, IForEach<T1> where T1 : unmanaged
    {
        var local = job;
        foreach (var chunk in GetChunks())
        {
            ref var c1 = ref MemoryMarshal.GetReference(chunk.GetSpan<T1>());
            for (var i = 0; i < chunk.Count; i++)
                local.Execute(ref Unsafe.Add(ref c1, i));
        }

        job = local;
    }

    public void ForEach<TJob, T1, T2>(ref TJob job)
        where TJob : struct, IForEach<T1, T2> where T1 : unmanaged where T2 : unmanaged
    {
        var local = job;
        foreach (var chunk in GetChunks())
        {
            ref var c1 = ref MemoryMarshal.GetReference(chunk.GetSpan<T1>());
            ref var c2 = ref MemoryMarshal.GetReference(chunk.GetSpan<T2>());
            for (var i = 0; i < chunk.Count; i++)
                local.Execute(ref Unsafe.Add(ref c1, i), ref Unsafe.Add(ref c2, i));
        }

        job = local;
    }

    public void ForEach<TJob, T1, T2, T3>(ref TJob job)
        where TJob : struct, IForEach<T1, T2, T3> where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
    {
        var local = job;
        foreach (var chunk in GetChunks())
        {
            ref var c1 = ref MemoryMarshal.GetReference(chunk.GetSpan<T1>());
            ref var c2 = ref MemoryMarshal.GetReference(chunk.GetSpan<T2>());
            ref var c3 = ref MemoryMarshal.GetReference(chunk.GetSpan<T3>());
            for (var i = 0; i < chunk.Count; i++)
                local.Execute(ref Unsafe.Add(ref c1, i), ref Unsafe.Add(ref c2, i), ref Unsafe.Add(ref c3, i));
        }

        job = local;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Matches archetypes the world created since the last call. Archetypes are never removed.</summary>
    private void Refresh()
    {
        var worldCount = _world.ArchetypeCount;
        for (; _checkedArchetypeCount < worldCount; _checkedArchetypeCount++)
        {
            var archetype = _world.GetArchetypeAt(_checkedArchetypeCount);
            if (!archetype.Mask.ContainsAll(_all) || archetype.Mask.ContainsAny(_none))
                continue;

            if (_archetypeCount == _archetypes.Length)
                Array.Resize(ref _archetypes, _archetypes.Length * 2);

            _archetypes[_archetypeCount++] = archetype;
        }
    }

    //**********************************************************
    //** enumeration
    //**********************************************************

    public readonly ref struct ChunkEnumerable
    {
        private readonly World _world;
        private readonly Archetype[] _archetypes;
        private readonly int _archetypeCount;

        internal ChunkEnumerable(World world, Archetype[] archetypes, int archetypeCount)
        {
            _world = world;
            _archetypes = archetypes;
            _archetypeCount = archetypeCount;
        }

        public ChunkEnumerator GetEnumerator() => new(_world, _archetypes, _archetypeCount);
    }

    /// <summary>
    /// Walks every non-empty chunk of every matching archetype. Blocks structural changes on the world from
    /// creation until <see cref="Dispose"/>, which <c>foreach</c> calls even on <c>break</c>, <c>return</c> or throw.
    /// </summary>
    public ref struct ChunkEnumerator
    {
        private readonly World _world;
        private readonly Archetype[] _archetypes;
        private readonly int _archetypeCount;
        private int _archetypeIndex;
        private int _chunkIndex;

        internal ChunkEnumerator(World world, Archetype[] archetypes, int archetypeCount)
        {
            _world = world;
            _archetypes = archetypes;
            _archetypeCount = archetypeCount;
            _chunkIndex = -1;
            world.BeginIteration();
        }

        public readonly void Dispose() => _world.EndIteration();

        public readonly EntityChunk Current
        {
            get
            {
                var archetype = _archetypes[_archetypeIndex];
                return new EntityChunk(archetype, archetype.GetChunk(_chunkIndex));
            }
        }

        public bool MoveNext()
        {
            while (_archetypeIndex < _archetypeCount)
            {
                if (++_chunkIndex < _archetypes[_archetypeIndex].ChunkCount)
                    return true;

                _archetypeIndex++;
                _chunkIndex = -1;
            }

            return false;
        }
    }
}
