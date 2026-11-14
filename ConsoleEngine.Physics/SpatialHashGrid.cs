using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Broadphase: a uniform grid over an unbounded world. Each body is put in every cell its bounding box touches.
/// <see cref="PhysicsSystem"/> keeps two: one for the moving bodies, rebuilt every step, and one for the static bodies,
/// rebuilt only when they change (as Box2D keeps a separate static tree). The cells are hashed into a table (Teschner et al. 2003) and sorted into it with a counting sort, so a
/// rebuild is O(bodies + cells) with no allocation once the arrays are big enough.
/// <para>
/// A uniform grid instead of Box2D's dynamic AABB tree: console games have bodies of similar size in a bounded area,
/// which is the case a grid handles best (Ericson, <i>Real-Time Collision Detection</i>, ch. 7). See
/// <c>docs/design/physics-plan.md</c> § 6.
/// </para>
/// </summary>
internal sealed class SpatialHashGrid
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int MinTableSize = 64;

    /// <summary>Multiplying is much cheaper than dividing, and <see cref="CellOf"/> runs for every bound.</summary>
    private readonly float _inverseCellSize;

    /// <summary>After a build, bucket <c>h</c> is <c>_entries[_bucketStarts[h] .. _bucketStarts[h + 1]]</c>.</summary>
    private int[] _bucketStarts = new int[MinTableSize + 1];

    private Entry[] _entries = new Entry[MinTableSize];

    /// <summary>The entries in body order before sorting, and the bucket of each, from the same build.</summary>
    private Entry[] _unsorted = new Entry[MinTableSize];

    private int[] _entryBuckets = new int[MinTableSize];
    private readonly ArrayBuffer<CellBox> _bounds = new();
    private int _tableMask = MinTableSize - 1;

    //**********************************************************
    //** ctor
    //**********************************************************

    public SpatialHashGrid(float cellSize)
    {
        if (!(cellSize > 0f) || !float.IsFinite(cellSize))
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Must be a positive, finite number.");

        CellSize = cellSize;
        _inverseCellSize = 1f / cellSize;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public float CellSize { get; }

    /// <summary>Cells filled by the last build, one per body per touched cell.</summary>
    public int EntryCount { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Puts the bodies <paramref name="indices"/> picks from <paramref name="bodies"/> in the grid.</summary>
    public void Build(ReadOnlySpan<BodyProxy> bodies, ReadOnlySpan<int> indices)
    {
        // Each body's bounds and cell range, worked out once.
        _bounds.Resize(indices.Length);
        var bounds = _bounds.AsSpan();
        var entryCount = 0;
        for (var i = 0; i < indices.Length; i++)
        {
            ref readonly var body = ref bodies[indices[i]];
            var extents = body.BoundsExtents;
            ref var box = ref bounds[i];
            box.Min = body.Center - extents;
            box.Max = body.Center + extents;
            box.MinX = CellOf(box.Min.X);
            box.MinY = CellOf(box.Min.Y);
            box.MaxX = CellOf(box.Max.X);
            box.MaxY = CellOf(box.Max.Y);
            entryCount += (box.MaxX - box.MinX + 1) * (box.MaxY - box.MinY + 1);
        }

        EnsureCapacity(entryCount);
        EntryCount = entryCount;
        var tableSize = _tableMask + 1;
        var starts = _bucketStarts;
        Array.Clear(starts, 0, tableSize + 1);

        // Counting sort: write the entries in body order with their bucket, counting per bucket; turn the counts into
        // bucket ends; then move each entry (backwards) to its bucket's end minus one, so afterwards every
        // _bucketStarts[h] is the start of bucket h. The hash is computed once per entry.
        var unsorted = _unsorted;
        var buckets = _entryBuckets;
        var k = 0;
        for (var i = 0; i < indices.Length; i++)
        {
            ref readonly var box = ref bounds[i];
            var body = indices[i];
            for (var y = box.MinY; y <= box.MaxY; y++)
            for (var x = box.MinX; x <= box.MaxX; x++)
            {
                var bucket = Hash(x, y);
                starts[bucket]++;
                buckets[k] = bucket;
                unsorted[k++] = new Entry(body, x, y, box.Min, box.Max);
            }
        }

        for (var h = 1; h < tableSize; h++)
            starts[h] += starts[h - 1];
        starts[tableSize] = entryCount;

        for (var e = entryCount - 1; e >= 0; e--)
            _entries[--starts[buckets[e]]] = unsorted[e];
    }

    /// <summary>
    /// Adds every pair of bodies whose bounding boxes overlap, exactly once, with <c>A &lt; B</c>. Uses the bounds of the
    /// last <see cref="Build"/>, stored in the entries so the scan reads them in order.
    /// </summary>
    public void FindPairs(ArrayBuffer<BodyPair> pairs)
    {
        var tableSize = _tableMask + 1;
        var entries = _entries;
        for (var h = 0; h < tableSize; h++)
        {
            var end = _bucketStarts[h + 1];
            for (var i = _bucketStarts[h]; i < end - 1; i++)
            {
                ref readonly var first = ref entries[i];
                for (var j = i + 1; j < end; j++)
                {
                    ref readonly var second = ref entries[j];

                    if (first.Body != second.Body && IsReportedHere(first, second))
                        pairs.Add(Pair(first.Body, second.Body));
                }
            }
        }
    }

    /// <summary>
    /// Adds every pair of a body in this grid and a body in <paramref name="other"/> whose bounding boxes overlap,
    /// exactly once, with <c>A &lt; B</c>. Both grids must have the same cell size.
    /// </summary>
    public void FindPairs(SpatialHashGrid other, ArrayBuffer<BodyPair> pairs)
    {
        var entries = _entries;
        for (var i = 0; i < EntryCount; i++)
        {
            ref readonly var first = ref entries[i];
            var bucket = other.GetBucket(first.CellX, first.CellY);
            for (var j = 0; j < bucket.Length; j++)
            {
                ref readonly var second = ref bucket[j];
                if (IsReportedHere(first, second))
                    pairs.Add(Pair(first.Body, second.Body));
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Same rounding as <see cref="GridWalker"/>: multiplication by the same inverse cell size.</summary>
    internal int CellOf(float coordinate) => (int)MathF.Floor(coordinate * _inverseCellSize);

    /// <summary>
    /// Whether two entries are in the same cell, their boxes overlap, and this cell reports the pair. Different cells
    /// can hash to the same bucket, and two bodies can share several cells: the pair is reported only in the cell that
    /// holds the corner of their overlap, which lies in exactly one cell of both.
    /// </summary>
    private bool IsReportedHere(in Entry first, in Entry second)
    {
        if (first.CellX != second.CellX || first.CellY != second.CellY)
            return false;

        var minX = MathF.Max(first.Min.X, second.Min.X);
        var minY = MathF.Max(first.Min.Y, second.Min.Y);
        var maxX = MathF.Min(first.Max.X, second.Max.X);
        var maxY = MathF.Min(first.Max.Y, second.Max.Y);
        return minX <= maxX && minY <= maxY && CellOf(minX) == first.CellX && CellOf(minY) == first.CellY;
    }

    private static BodyPair Pair(int a, int b) => a < b ? new BodyPair(a, b) : new BodyPair(b, a);

    private int Hash(int x, int y) => (int)(((uint)x * 73856093u) ^ ((uint)y * 19349663u)) & _tableMask;

    /// <summary>
    /// The entries of the bucket cell (<paramref name="x"/>, <paramref name="y"/>) hashes to, from the last build. Other
    /// cells can share the bucket: check each entry's cell.
    /// </summary>
    internal ReadOnlySpan<Entry> GetBucket(int x, int y)
    {
        var bucket = Hash(x, y);
        var start = _bucketStarts[bucket];
        return _entries.AsSpan(start, _bucketStarts[bucket + 1] - start);
    }

    /// <summary>Entries fit, and the table has about two buckets per entry so most buckets hold one cell.</summary>
    private void EnsureCapacity(int entryCount)
    {
        if (entryCount > _entries.Length)
        {
            var size = Math.Max(entryCount, _entries.Length * 2);
            _entries = new Entry[size];
            _unsorted = new Entry[size];
            _entryBuckets = new int[size];
        }

        var tableSize = _tableMask + 1;
        if (tableSize >= entryCount * 2)
            return;

        while (tableSize < entryCount * 2)
            tableSize *= 2;

        _bucketStarts = new int[tableSize + 1];
        _tableMask = tableSize - 1;
    }

    /// <summary>A body in one cell, with the body's grown bounds so a pair test doesn't look the body up.</summary>
    internal readonly struct Entry(int body, int cellX, int cellY, Vector2 min, Vector2 max)
    {
        public readonly int Body = body;
        public readonly int CellX = cellX;
        public readonly int CellY = cellY;
        public readonly Vector2 Min = min;
        public readonly Vector2 Max = max;
    }

    /// <summary>A body's grown bounds and the cells they cover.</summary>
    private struct CellBox
    {
        public Vector2 Min;
        public Vector2 Max;
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;
    }
}

/// <summary>Two bodies by their index in the step's body list, <see cref="A"/> &lt; <see cref="B"/>.</summary>
internal readonly struct BodyPair(int a, int b)
{
    public readonly int A = a;
    public readonly int B = b;
}
