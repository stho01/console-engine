using System;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// Broadphase: a uniform grid over an unbounded world, rebuilt every step. Each body is put in every cell its bounding
/// box touches. The cells are hashed into a table (Teschner et al. 2003) and sorted into it with a counting sort, so a
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

    private readonly float _inverseCellSize;

    /// <summary>After a build, bucket <c>h</c> is <c>_entries[_bucketStarts[h] .. _bucketStarts[h + 1]]</c>.</summary>
    private int[] _bucketStarts = new int[MinTableSize + 1];

    private Entry[] _entries = new Entry[MinTableSize];
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

    public void Build(ReadOnlySpan<BodyProxy> bodies)
    {
        var entryCount = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            GetCellRange(bodies[i], out var minX, out var minY, out var maxX, out var maxY);
            entryCount += (maxX - minX + 1) * (maxY - minY + 1);
        }

        EnsureCapacity(entryCount);
        EntryCount = entryCount;

        // Counting sort. Count per bucket, turn counts into bucket ends, then fill backwards: each entry decrements its
        // bucket's end, so afterwards every _bucketStarts[h] is the start of bucket h.
        var tableSize = _tableMask + 1;
        var starts = _bucketStarts;
        Array.Clear(starts, 0, tableSize + 1);

        for (var i = 0; i < bodies.Length; i++)
        {
            GetCellRange(bodies[i], out var minX, out var minY, out var maxX, out var maxY);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
                starts[Hash(x, y)]++;
        }

        for (var h = 1; h < tableSize; h++)
            starts[h] += starts[h - 1];
        starts[tableSize] = entryCount;

        for (var i = bodies.Length - 1; i >= 0; i--)
        {
            GetCellRange(bodies[i], out var minX, out var minY, out var maxX, out var maxY);
            for (var y = maxY; y >= minY; y--)
            for (var x = maxX; x >= minX; x--)
                _entries[--starts[Hash(x, y)]] = new Entry(i, x, y);
        }
    }

    /// <summary>
    /// Adds every pair of bodies whose bounding boxes overlap, exactly once, with <c>A &lt; B</c>. Uses the bodies of
    /// the last <see cref="Build"/>.
    /// </summary>
    public void FindPairs(ReadOnlySpan<BodyProxy> bodies, ArrayBuffer<BodyPair> pairs)
    {
        var tableSize = _tableMask + 1;
        for (var h = 0; h < tableSize; h++)
        {
            var end = _bucketStarts[h + 1];
            for (var i = _bucketStarts[h]; i < end - 1; i++)
            {
                var first = _entries[i];
                for (var j = i + 1; j < end; j++)
                {
                    var second = _entries[j];

                    // Different cells that hash to the same bucket. The pair, if any, is found in its own cell.
                    if (first.CellX != second.CellX || first.CellY != second.CellY || first.Body == second.Body)
                        continue;

                    ref readonly var a = ref bodies[first.Body];
                    ref readonly var b = ref bodies[second.Body];
                    var boundsA = a.BoundsExtents;
                    var boundsB = b.BoundsExtents;
                    var minX = MathF.Max(a.Center.X - boundsA.X, b.Center.X - boundsB.X);
                    var minY = MathF.Max(a.Center.Y - boundsA.Y, b.Center.Y - boundsB.Y);
                    var maxX = MathF.Min(a.Center.X + boundsA.X, b.Center.X + boundsB.X);
                    var maxY = MathF.Min(a.Center.Y + boundsA.Y, b.Center.Y + boundsB.Y);
                    if (minX > maxX || minY > maxY)
                        continue;

                    // Two bodies can share several cells. Report the pair only in the cell that holds the corner of
                    // their overlap, which lies in exactly one cell of both.
                    if (CellOf(minX) != first.CellX || CellOf(minY) != first.CellY)
                        continue;

                    pairs.Add(first.Body < second.Body
                        ? new BodyPair(first.Body, second.Body)
                        : new BodyPair(second.Body, first.Body));
                }
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void GetCellRange(in BodyProxy body, out int minX, out int minY, out int maxX, out int maxY)
    {
        var bounds = body.BoundsExtents;
        minX = CellOf(body.Center.X - bounds.X);
        minY = CellOf(body.Center.Y - bounds.Y);
        maxX = CellOf(body.Center.X + bounds.X);
        maxY = CellOf(body.Center.Y + bounds.Y);
    }

    private int CellOf(float coordinate) => (int)MathF.Floor(coordinate * _inverseCellSize);

    private int Hash(int x, int y) => (int)(((uint)x * 73856093u) ^ ((uint)y * 19349663u)) & _tableMask;

    /// <summary>Entries fit, and the table has about two buckets per entry so most buckets hold one cell.</summary>
    private void EnsureCapacity(int entryCount)
    {
        if (entryCount > _entries.Length)
            _entries = new Entry[Math.Max(entryCount, _entries.Length * 2)];

        var tableSize = _tableMask + 1;
        if (tableSize >= entryCount * 2)
            return;

        while (tableSize < entryCount * 2)
            tableSize *= 2;

        _bucketStarts = new int[tableSize + 1];
        _tableMask = tableSize - 1;
    }

    private readonly struct Entry(int body, int cellX, int cellY)
    {
        public readonly int Body = body;
        public readonly int CellX = cellX;
        public readonly int CellY = cellY;
    }
}

/// <summary>Two bodies by their index in the step's body list, <see cref="A"/> &lt; <see cref="B"/>.</summary>
internal readonly struct BodyPair(int a, int b)
{
    public readonly int A = a;
    public readonly int B = b;
}
