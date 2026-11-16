using System.Collections.Generic;
using System.Numerics;

namespace Doom.Rendering.Meshes;

/// <summary>Helpers for convex polygons in map space (x east, y north), as lists of corners.</summary>
public static class ConvexPolygon
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Sutherland–Hodgman: writes into <paramref name="result"/> the part of <paramref name="polygon"/> where
    /// <c>Dot(normal, p) &gt;= distance</c>. Keeps the corner order.
    /// </summary>
    public static void Clip(List<Vector2> polygon, List<Vector2> result, Vector2 normal, float distance)
    {
        result.Clear();
        var count = polygon.Count;
        if (count == 0)
            return;

        var previous = polygon[count - 1];
        var previousSide = Vector2.Dot(normal, previous) - distance;
        for (var i = 0; i < count; i++)
        {
            var current = polygon[i];
            var side = Vector2.Dot(normal, current) - distance;
            if ((side >= 0) != (previousSide >= 0))
                result.Add(Vector2.Lerp(previous, current, previousSide / (previousSide - side)));
            if (side >= 0)
                result.Add(current);

            previous = current;
            previousSide = side;
        }
    }

    /// <summary>
    /// Keeps the part on the right of the directed line through <paramref name="start"/> along
    /// <paramref name="direction"/>: Doom's front side of a line, seg or node partition.
    /// </summary>
    public static void ClipToRight(List<Vector2> polygon, List<Vector2> result, Vector2 start, Vector2 direction)
    {
        var normal = new Vector2(direction.Y, -direction.X);
        Clip(polygon, result, normal, Vector2.Dot(normal, start));
    }

    /// <summary>Twice the signed area: positive when the corners go counter-clockwise.</summary>
    public static float DoubleArea(List<Vector2> polygon)
    {
        var area = 0f;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            area += polygon[j].X * polygon[i].Y - polygon[i].X * polygon[j].Y;
        return area;
    }
}
