using System;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

// IPhysicsQueries. The step's shapes and proxies are at the start-of-step transforms and the grid includes each body's
// margin. The first query after a step moves the shapes to the end-of-step transforms, recomputes their bounds and
// rebuilds the moving bodies' grid without margins, so a game that never queries pays nothing. Static bodies don't
// move and have no margin, so their grid is used as the step left it. Queries look in both grids.
public sealed partial class PhysicsSystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private bool _queryStateIsCurrent;

    /// <summary>Per body: the query that last looked at it, so a body in several cells is tested once per query.</summary>
    private int[] _queryStamps = new int[64];

    private int _queryStamp;

    //**********************************************************
    //** public methods
    //**********************************************************

    public bool Raycast(Vector2 origin, Vector2 translation, uint mask, out CastHit hit)
    {
        EnsureQueryState();
        hit = default;
        var best = float.PositiveInfinity;
        RaycastBodies(origin, translation, mask, ref best, ref hit);
        if (TileGrid != null && (TileGrid.Category & mask) != 0)
            RaycastTiles(TileGrid, origin, translation, ref best, ref hit);

        return best <= 1f;
    }

    public bool CastBox(Vector2 center, Vector2 halfExtents, Vector2 translation, uint mask, Entity ignore,
        out CastHit hit)
    {
        EnsureQueryState();
        hit = default;
        var best = float.PositiveInfinity;
        var min = Vector2.Min(center, center + translation) - halfExtents;
        var max = Vector2.Max(center, center + translation) + halfExtents;
        CastBoxBodies(center, halfExtents, translation, mask, ignore, min, max, ref best, ref hit);
        if (TileGrid != null && (TileGrid.Category & mask) != 0)
            CastBoxTiles(TileGrid, center, halfExtents, translation, min, max, ref best, ref hit);

        return best <= 1f;
    }

    public int OverlapBox(Vector2 center, Vector2 halfExtents, uint mask, Span<Entity> results)
    {
        var shape = BodyShape.Unrotated(center, ShapeType.Box);
        shape.HalfExtents = halfExtents;
        return Overlap(shape, center, halfExtents, mask, results);
    }

    public int OverlapCircle(Vector2 center, float radius, uint mask, Span<Entity> results)
    {
        var shape = BodyShape.Unrotated(center, ShapeType.Circle);
        shape.Radius = radius;
        return Overlap(shape, center, new Vector2(radius, radius), mask, results);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void EnsureQueryState()
    {
        if (_queryStateIsCurrent)
            return;

        var proxies = _proxies.AsSpan();
        var shapes = _shapes.AsSpan();
        var states = _states.AsSpan();
        var sims = _sims.AsSpan();
        for (var i = 0; i < proxies.Length; i++)
        {
            if (sims[i].Type != BodyType.Static)
            {
                ref readonly var state = ref states[i];
                ref var shape = ref shapes[i];
                shape.Position += state.DeltaPosition;
                var turn = state.DeltaRotation;
                (shape.Cos, shape.Sin) = (shape.Cos * turn.X - shape.Sin * turn.Y, shape.Sin * turn.X + shape.Cos * turn.Y);
                ComputeBounds(shape, out proxies[i].Center, out proxies[i].Extents, out _);
            }

            proxies[i].Margin = 0f;
        }

        _grid.Build(proxies, _movingBodies.AsSpan());
        if (_queryStamps.Length < proxies.Length)
            _queryStamps = new int[Math.Max(proxies.Length, _queryStamps.Length * 2)];

        _queryStateIsCurrent = true;
    }

    /// <summary>0: the moving bodies' grid, 1: the static bodies' grid.</summary>
    private SpatialHashGrid Grid(int index) => index == 0 ? _grid : _staticGrid;

    private int NextQueryStamp()
    {
        if (++_queryStamp == int.MaxValue)
        {
            Array.Clear(_queryStamps);
            _queryStamp = 1;
        }

        return _queryStamp;
    }

    /// <summary>A body a query may report: not a sensor, in the mask, not ignored, and not yet tested by it.</summary>
    private bool IsCandidate(int body, int stamp, uint mask, Entity ignore)
    {
        if (_queryStamps[body] == stamp)
            return false;

        _queryStamps[body] = stamp;
        ref readonly var proxy = ref _proxies[body];
        return !proxy.IsSensor && (proxy.Category & mask) != 0 && _entities[body] != ignore;
    }

    /// <summary>Walks the body grid's cells along the ray until a hit closer than the next cell is known.</summary>
    private void RaycastBodies(Vector2 origin, Vector2 translation, uint mask, ref float best, ref CastHit hit)
    {
        if (_proxies.Count == 0)
            return;

        var stamp = NextQueryStamp();
        var cellSize = new Vector2(_grid.CellSize, _grid.CellSize);
        var walker = new GridWalker(origin, translation, cellSize, Vector2.Zero);
        do
        {
            if (walker.Enter > best)
                return;

            for (var grid = 0; grid < 2; grid++)
            {
                var bucket = Grid(grid).GetBucket(walker.X, walker.Y);
                for (var i = 0; i < bucket.Length; i++)
                {
                    var entry = bucket[i];
                    if (entry.CellX != walker.X || entry.CellY != walker.Y ||
                        !IsCandidate(entry.Body, stamp, mask, Entity.Null))
                        continue;

                    if (RaycastShape(_shapes[entry.Body], origin, translation, out var fraction, out var normal) &&
                        fraction < best)
                    {
                        best = fraction;
                        hit = new CastHit(_entities[entry.Body], default, origin + translation * fraction, normal,
                            fraction);
                    }
                }
            }
        } while (walker.Next());
    }

    private bool RaycastShape(in BodyShape shape, Vector2 origin, Vector2 translation, out float fraction,
        out Vector2 normal)
    {
        if (shape.Shape == ShapeType.Circle)
        {
            return RayCast.Circle(origin, translation, shape.ToWorld(shape.Offset), shape.Radius, false, out fraction,
                out normal);
        }

        if (shape.Shape == ShapeType.Box && !shape.IsRotated)
        {
            return RayCast.Box(origin, translation, shape.Position + shape.Offset, shape.HalfExtents, false,
                out fraction, out normal);
        }

        shape.ToWorldPolygon(Polygons, out var polygon);
        ReadOnlySpan<Vector2> points = polygon.Points;
        return RayCast.Polygon(origin, translation, points[..polygon.Count], false, out fraction, out normal);
    }

    /// <summary>
    /// Walks the tile grid along the ray. Solid tiles the ray starts in are skipped; the first solid tile entered after
    /// that is the hit, on the face the ray crossed.
    /// </summary>
    private static void RaycastTiles(TileGrid tiles, Vector2 origin, Vector2 translation, ref float best,
        ref CastHit hit)
    {
        var walker = new GridWalker(origin, translation, tiles.TileSize, tiles.Origin);
        var leftStart = !tiles.IsSolid(walker.X, walker.Y);
        while (walker.Next())
        {
            if (walker.Enter > best)
                return;

            if (!tiles.IsSolid(walker.X, walker.Y))
            {
                leftStart = true;
                continue;
            }

            if (!leftStart)
                continue;

            best = walker.Enter;
            hit = new CastHit(Entity.Null, new Point(walker.X, walker.Y), origin + translation * walker.Enter,
                walker.EnterNormal, walker.Enter);
            return;
        }
    }

    private void CastBoxBodies(Vector2 center, Vector2 halfExtents, Vector2 translation, uint mask, Entity ignore,
        Vector2 min, Vector2 max, ref float best, ref CastHit hit)
    {
        if (_proxies.Count == 0)
            return;

        var stamp = NextQueryStamp();
        var firstX = _grid.CellOf(min.X);
        var lastX = _grid.CellOf(max.X);
        var firstY = _grid.CellOf(min.Y);
        var lastY = _grid.CellOf(max.Y);
        for (var y = firstY; y <= lastY; y++)
        for (var x = firstX; x <= lastX; x++)
        for (var grid = 0; grid < 2; grid++)
        {
            var bucket = Grid(grid).GetBucket(x, y);
            for (var i = 0; i < bucket.Length; i++)
            {
                var entry = bucket[i];
                if (entry.CellX != x || entry.CellY != y || !IsCandidate(entry.Body, stamp, mask, ignore))
                    continue;

                if (CastBoxAgainst(_shapes[entry.Body], center, halfExtents, translation, out var fraction,
                        out var normal) && fraction < best)
                {
                    best = fraction;
                    hit = new CastHit(_entities[entry.Body], default, center + translation * fraction, normal, fraction);
                }
            }
        }
    }

    /// <summary>
    /// A box cast is a ray from the box's centre against the shape grown by the box (Minkowski sum): a box gives a bigger
    /// box, a circle a box with rounded corners, a turned box or polygon the hull of its corners plus the box's corners.
    /// </summary>
    private bool CastBoxAgainst(in BodyShape shape, Vector2 center, Vector2 halfExtents, Vector2 translation,
        out float fraction, out Vector2 normal)
    {
        if (shape.Shape == ShapeType.Circle)
        {
            return RayCast.RoundedBox(center, translation, shape.ToWorld(shape.Offset), halfExtents, shape.Radius, true,
                out fraction, out normal);
        }

        if (shape.Shape == ShapeType.Box && !shape.IsRotated)
        {
            return RayCast.Box(center, translation, shape.Position + shape.Offset, shape.HalfExtents + halfExtents, true,
                out fraction, out normal);
        }

        shape.ToWorldPolygon(Polygons, out var polygon);
        Span<Vector2> sum = stackalloc Vector2[ConvexPolygon.MaxVertices * 4];
        var count = 0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var point = polygon.Points[i];
            sum[count++] = point + new Vector2(-halfExtents.X, -halfExtents.Y);
            sum[count++] = point + new Vector2(halfExtents.X, -halfExtents.Y);
            sum[count++] = point + new Vector2(halfExtents.X, halfExtents.Y);
            sum[count++] = point + new Vector2(-halfExtents.X, halfExtents.Y);
        }

        Span<Vector2> hull = stackalloc Vector2[ConvexPolygon.MaxVertices * 8];
        var corners = ConvexPolygon.HullInto(sum[..count], hull);
        return RayCast.Polygon(center, translation, hull[..corners], true, out fraction, out normal);
    }

    /// <summary>
    /// Tests every solid tile in the swept box. A face that touches another solid tile is inside the wall, so a hit on it
    /// is ignored: otherwise a box sliding along a row of tiles would catch on the seams (the "ghost collision" problem
    /// Box2D solves with chain shapes).
    /// </summary>
    private static void CastBoxTiles(TileGrid tiles, Vector2 center, Vector2 halfExtents, Vector2 translation,
        Vector2 min, Vector2 max, ref float best, ref CastHit hit)
    {
        var firstColumn = Math.Max(tiles.ColumnOf(min.X), 0);
        var firstRow = Math.Max(tiles.RowOf(min.Y), 0);
        var lastColumn = Math.Min(tiles.ColumnOf(max.X), tiles.Columns - 1);
        var lastRow = Math.Min(tiles.RowOf(max.Y), tiles.Rows - 1);
        var tileExtents = tiles.TileSize * .5f;
        for (var row = firstRow; row <= lastRow; row++)
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            if (!tiles.IsSolid(column, row))
                continue;

            var tileCenter = tiles.TileMin(column, row) + tileExtents;
            if (!RayCast.Box(center, translation, tileCenter, tileExtents + halfExtents, true, out var fraction,
                    out var normal) || fraction >= best)
                continue;

            if (tiles.IsSolid(column + (int)normal.X, row + (int)normal.Y))
                continue;

            best = fraction;
            hit = new CastHit(Entity.Null, new Point(column, row), center + translation * fraction, normal, fraction);
        }
    }

    private int Overlap(in BodyShape shape, Vector2 center, Vector2 extents, uint mask, Span<Entity> results)
    {
        EnsureQueryState();
        if (_proxies.Count == 0 || results.IsEmpty)
            return 0;

        var stamp = NextQueryStamp();
        var count = 0;
        var firstX = _grid.CellOf(center.X - extents.X);
        var lastX = _grid.CellOf(center.X + extents.X);
        var firstY = _grid.CellOf(center.Y - extents.Y);
        var lastY = _grid.CellOf(center.Y + extents.Y);
        for (var y = firstY; y <= lastY; y++)
        for (var x = firstX; x <= lastX; x++)
        for (var grid = 0; grid < 2; grid++)
        {
            var bucket = Grid(grid).GetBucket(x, y);
            for (var i = 0; i < bucket.Length; i++)
            {
                var entry = bucket[i];
                if (entry.CellX != x || entry.CellY != y || !IsCandidate(entry.Body, stamp, mask, Entity.Null))
                    continue;

                Collision.Collide(shape, _shapes[entry.Body], Polygons, 0f, out var manifold);
                if (manifold.PointCount == 0 || manifold.MinSeparation > 0f)
                    continue;

                results[count++] = _entities[entry.Body];
                if (count == results.Length)
                    return count;
            }
        }

        return count;
    }
}
