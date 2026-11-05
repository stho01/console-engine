using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConsoleEngine.Ecs;

/// <summary>
/// All entities with exactly the same set of components. Their data lives in chunks with one shared layout, and the
/// archetype keeps its rows dense across chunks: every chunk but the last is full.
/// </summary>
internal sealed unsafe class Archetype
{
    //**********************************************************
    //** fields
    //**********************************************************

    public readonly int Index;
    public readonly ComponentMask Mask;
    public readonly int ChunkCapacity;

    /// <summary>Components with data (no tags), ascending by id. Parallel to the offset and size arrays.</summary>
    public readonly int[] ColumnIds;
    public readonly int[] ColumnOffsets;
    public readonly int[] ColumnSizes;

    /// <summary>Byte offset of a component's column in a chunk, indexed by component id. -1 if absent or a tag.</summary>
    private readonly int[] _offsetById;

    private readonly Dictionary<int, Archetype> _addEdges = new();
    private readonly Dictionary<int, Archetype> _removeEdges = new();
    private Chunk[] _chunks = new Chunk[1];
    private int _chunkCount;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Archetype(int index, ComponentMask mask)
    {
        Index = index;
        Mask = mask;

        var ids = mask.ToIds();
        var columns = new List<int>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            if (ComponentRegistry.SizeOf(ids[i]) > 0)
                columns.Add(ids[i]);
        }

        ColumnIds = columns.ToArray();
        ColumnSizes = new int[ColumnIds.Length];
        for (var i = 0; i < ColumnIds.Length; i++)
            ColumnSizes[i] = ComponentRegistry.SizeOf(ColumnIds[i]);

        ChunkCapacity = ComputeCapacity(ColumnSizes);
        ColumnOffsets = ComputeOffsets(ColumnSizes, ChunkCapacity);

        _offsetById = new int[ColumnIds.Length == 0 ? 0 : ColumnIds[^1] + 1];
        Array.Fill(_offsetById, -1);
        for (var i = 0; i < ColumnIds.Length; i++)
            _offsetById[ColumnIds[i]] = ColumnOffsets[i];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int EntityCount { get; private set; }
    public int ChunkCount => _chunkCount;

    //**********************************************************
    //** public methods
    //**********************************************************

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref Chunk GetChunk(int index) => ref _chunks[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetOffset(int componentId) =>
        (uint)componentId < (uint)_offsetById.Length ? _offsetById[componentId] : -1;

    public bool TryGetAddEdge(int componentId, out Archetype target) =>
        _addEdges.TryGetValue(componentId, out target);

    public bool TryGetRemoveEdge(int componentId, out Archetype target) =>
        _removeEdges.TryGetValue(componentId, out target);

    public void SetAddEdge(int componentId, Archetype target) => _addEdges[componentId] = target;
    public void SetRemoveEdge(int componentId, Archetype target) => _removeEdges[componentId] = target;

    /// <summary>Appends a row for the entity. Component data in the row is uninitialized.</summary>
    public void Allocate(Entity entity, ChunkPool pool, out int chunkIndex, out int row)
    {
        AllocateRange(1, pool, out chunkIndex, out row);
        _chunks[chunkIndex].Entities[row] = entity;
    }

    /// <summary>
    /// Appends up to <paramref name="requested"/> rows in the last chunk and returns how many it got. The caller
    /// writes the entities and component data of those rows.
    /// </summary>
    public int AllocateRange(int requested, ChunkPool pool, out int chunkIndex, out int firstRow)
    {
        if (_chunkCount == 0 || _chunks[_chunkCount - 1].Count == ChunkCapacity)
            AddChunk(pool);

        chunkIndex = _chunkCount - 1;
        ref var chunk = ref _chunks[chunkIndex];
        firstRow = chunk.Count;

        var count = Math.Min(requested, ChunkCapacity - chunk.Count);
        chunk.Count += count;
        EntityCount += count;
        return count;
    }

    /// <summary>
    /// Removes a row by moving the archetype's last row into it (swap-back). Returns the entity that moved into
    /// the row, or <see cref="Entity.Null"/> if the removed row was the last one.
    /// </summary>
    public Entity RemoveAt(int chunkIndex, int row, ChunkPool pool)
    {
        var lastIndex = _chunkCount - 1;
        ref var last = ref _chunks[lastIndex];
        var lastRow = last.Count - 1;
        var moved = Entity.Null;

        if (chunkIndex != lastIndex || row != lastRow)
        {
            ref var chunk = ref _chunks[chunkIndex];
            moved = last.Entities[lastRow];
            chunk.Entities[row] = moved;
            for (var i = 0; i < ColumnIds.Length; i++)
            {
                var size = ColumnSizes[i];
                var column = ColumnOffsets[i];
                Unsafe.CopyBlockUnaligned(
                    chunk.Memory + column + row * size, last.Memory + column + lastRow * size, (uint)size);
            }
        }

        last.Count--;
        EntityCount--;
        if (last.Count == 0)
        {
            pool.Return(last.Memory);
            last = default;
            _chunkCount--;
        }

        return moved;
    }

    /// <summary>Copies every component both archetypes have from one row to another.</summary>
    public static void CopyShared(Archetype from, ref Chunk fromChunk, int fromRow, Archetype to, ref Chunk toChunk,
        int toRow)
    {
        for (var i = 0; i < from.ColumnIds.Length; i++)
        {
            var toColumn = to.GetOffset(from.ColumnIds[i]);
            if (toColumn < 0)
                continue;

            var size = from.ColumnSizes[i];
            Unsafe.CopyBlockUnaligned(
                toChunk.Memory + toColumn + toRow * size,
                fromChunk.Memory + from.ColumnOffsets[i] + fromRow * size,
                (uint)size);
        }
    }

    /// <summary>Frees the native memory of all chunks in use. Only called when the world is disposed.</summary>
    public void FreeChunks()
    {
        for (var i = 0; i < _chunkCount; i++)
        {
            NativeMemory.AlignedFree(_chunks[i].Memory);
            _chunks[i] = default;
        }

        _chunkCount = 0;
        EntityCount = 0;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void AddChunk(ChunkPool pool)
    {
        if (_chunkCount == _chunks.Length)
            Array.Resize(ref _chunks, _chunks.Length * 2);

        _chunks[_chunkCount++] = new Chunk { Memory = pool.Rent() };
    }

    private static int ComputeCapacity(int[] sizes)
    {
        var rowSize = sizeof(Entity);
        for (var i = 0; i < sizes.Length; i++)
            rowSize += sizes[i];

        var capacity = Chunk.Size / rowSize;
        while (capacity > 0 && LayoutEnd(sizes, capacity) > Chunk.Size)
            capacity--;

        if (capacity == 0)
            EcsThrowHelper.ThrowRowTooLarge(rowSize);

        return capacity;
    }

    private static int[] ComputeOffsets(int[] sizes, int capacity)
    {
        var offsets = new int[sizes.Length];
        var end = capacity * sizeof(Entity);
        for (var i = 0; i < sizes.Length; i++)
        {
            offsets[i] = AlignToCacheLine(end);
            end = offsets[i] + capacity * sizes[i];
        }

        return offsets;
    }

    private static int LayoutEnd(int[] sizes, int capacity)
    {
        var end = capacity * sizeof(Entity);
        for (var i = 0; i < sizes.Length; i++)
            end = AlignToCacheLine(end) + capacity * sizes[i];

        return end;
    }

    private static int AlignToCacheLine(int offset) => (offset + Chunk.CacheLineSize - 1) & ~(Chunk.CacheLineSize - 1);
}
