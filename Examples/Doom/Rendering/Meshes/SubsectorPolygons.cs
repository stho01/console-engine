using System;
using System.Collections.Generic;
using System.Numerics;
using Doom.Maps;

namespace Doom.Rendering.Meshes;

/// <summary>
/// The floor shape of every subsector, which the WAD doesn't store: a subsector's segs only cover its walls, the
/// rest of its edge is the node partitions above it. So a box round the map is cut by each partition on the way
/// down the BSP tree, and at each subsector by its segs' lines (the subsector is on their right). The result is
/// convex and counter-clockwise. Built once per map; allocates.
/// </summary>
public static class SubsectorPolygons
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Map units added round the map's bounds for the starting box.</summary>
    private const float Margin = 64;

    /// <summary>Polygons smaller than this (twice the area, square map units) are dropped as slivers.</summary>
    private const float MinDoubleArea = 0.01f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>One polygon per subsector, in <see cref="Map.Subsectors"/> order; empty when nothing is left.</summary>
    public static Vector2[][] Build(Map map)
    {
        var polygons = new Vector2[map.Subsectors.Length][];
        Array.Fill(polygons, []);
        if (map.Subsectors.Length == 0)
            return polygons;

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var line in map.Lines)
        {
            minX = MathF.Min(minX, line.MinX);
            minY = MathF.Min(minY, line.MinY);
            maxX = MathF.Max(maxX, line.MaxX);
            maxY = MathF.Max(maxY, line.MaxY);
        }

        minX -= Margin;
        minY -= Margin;
        maxX += Margin;
        maxY += Margin;
        List<Vector2> box = [new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY)];
        Split(map, map.Root, box, polygons);
        return polygons;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Split(Map map, int child, List<Vector2> polygon, Vector2[][] polygons)
    {
        if ((child & Map.SubsectorFlag) != 0)
        {
            var index = child & ~Map.SubsectorFlag;
            if (index < polygons.Length)
                polygons[index] = Finish(map, map.Subsectors[index], polygon);
            return;
        }

        var node = map.Nodes[child];
        var start = new Vector2(node.X, node.Y);
        var direction = new Vector2(node.Dx, node.Dy);
        var front = new List<Vector2>(polygon.Count + 1);
        var back = new List<Vector2>(polygon.Count + 1);
        ConvexPolygon.ClipToRight(polygon, front, start, direction);
        ConvexPolygon.ClipToRight(polygon, back, start, -direction);
        Split(map, node.Front, front, polygons);
        Split(map, node.Back, back, polygons);
    }

    private static Vector2[] Finish(Map map, Subsector subsector, List<Vector2> polygon)
    {
        var current = new List<Vector2>(polygon);
        var next = new List<Vector2>(polygon.Count + 1);
        var end = Math.Min(subsector.FirstSeg + subsector.SegCount, map.Segs.Length);
        for (var i = subsector.FirstSeg; i < end; i++)
        {
            var seg = map.Segs[i];
            var direction = new Vector2(seg.V2.X - seg.V1.X, seg.V2.Y - seg.V1.Y);
            if (direction == Vector2.Zero)
                continue;

            ConvexPolygon.ClipToRight(current, next, new Vector2(seg.V1.X, seg.V1.Y), direction);
            (current, next) = (next, current);
        }

        var area = ConvexPolygon.DoubleArea(current);
        if (current.Count < 3 || MathF.Abs(area) < MinDoubleArea)
            return [];
        if (area < 0)
            current.Reverse();
        return current.ToArray();
    }
}
