using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// A convex polygon of up to <see cref="MaxVertices"/> vertices in a body's local space, wound so that
/// <c>cross(v[i+1] - v[i], v[i+2] - v[i+1]) &gt; 0</c>, with the outward normal of each edge. Fixed size and unmanaged,
/// so it can be built on the stack (boxes, tiles) or kept in a <see cref="PolygonLibrary"/>.
/// </summary>
internal struct ConvexPolygon
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Box2D's limit.</summary>
    public const int MaxVertices = 8;

    public Vertices Points;

    /// <summary><c>Normals[i]</c> belongs to the edge from <c>Points[i]</c> to <c>Points[i + 1]</c>.</summary>
    public Vertices Normals;

    public int Count;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>An axis-aligned box around <paramref name="center"/>.</summary>
    public static ConvexPolygon Box(Vector2 center, Vector2 halfExtents)
    {
        var polygon = new ConvexPolygon { Count = 4 };
        polygon.Points[0] = center + new Vector2(-halfExtents.X, -halfExtents.Y);
        polygon.Points[1] = center + new Vector2(halfExtents.X, -halfExtents.Y);
        polygon.Points[2] = center + new Vector2(halfExtents.X, halfExtents.Y);
        polygon.Points[3] = center + new Vector2(-halfExtents.X, halfExtents.Y);
        polygon.Normals[0] = new Vector2(0f, -1f);
        polygon.Normals[1] = new Vector2(1f, 0f);
        polygon.Normals[2] = new Vector2(0f, 1f);
        polygon.Normals[3] = new Vector2(-1f, 0f);
        return polygon;
    }

    /// <summary>
    /// The convex hull of <paramref name="points"/> (Andrew's monotone chain). Collinear and duplicate points are dropped.
    /// Throws if fewer than 3 points remain or the hull has more than <see cref="MaxVertices"/> corners.
    /// </summary>
    public static ConvexPolygon Hull(ReadOnlySpan<Vector2> points)
    {
        if (points.Length < 3)
            throw new ArgumentException("A polygon needs at least 3 points.", nameof(points));

        var hull = new Vector2[points.Length * 2];
        var count = HullInto(points, hull);
        if (count < 3)
            throw new ArgumentException("The points don't span an area.", nameof(points));
        if (count > MaxVertices)
            throw new ArgumentException($"The hull has {count} corners; at most {MaxVertices} are supported.",
                nameof(points));

        var polygon = new ConvexPolygon { Count = count };
        for (var i = 0; i < count; i++)
            polygon.Points[i] = hull[i];
        polygon.ComputeNormals();
        return polygon;
    }

    /// <summary>The polygon rotated by (<paramref name="cos"/>, <paramref name="sin"/>) and moved to
    /// <paramref name="position"/>.</summary>
    public readonly ConvexPolygon Transform(Vector2 position, float cos, float sin)
    {
        var result = new ConvexPolygon { Count = Count };
        for (var i = 0; i < Count; i++)
        {
            result.Points[i] = position + Rotate(Points[i], cos, sin);
            result.Normals[i] = Rotate(Normals[i], cos, sin);
        }

        return result;
    }

    /// <summary>Area, centroid and second moment of area about the origin (Box2D's <c>b2ComputePolygonMass</c>).</summary>
    public readonly void ComputeMass(out float area, out Vector2 centroid, out float inertiaAboutOrigin)
    {
        area = 0f;
        centroid = Vector2.Zero;
        inertiaAboutOrigin = 0f;
        var reference = Points[0];
        for (var i = 1; i < Count - 1; i++)
        {
            var e1 = Points[i] - reference;
            var e2 = Points[i + 1] - reference;
            var d = Cross(e1, e2);
            var triangleArea = .5f * d;
            area += triangleArea;
            centroid += triangleArea * (e1 + e2) / 3f;

            var intX2 = e1.X * e1.X + e2.X * e1.X + e2.X * e2.X;
            var intY2 = e1.Y * e1.Y + e2.Y * e1.Y + e2.Y * e2.Y;
            inertiaAboutOrigin += .25f / 3f * d * (intX2 + intY2);
        }

        centroid = centroid / area + reference;

        // The sums above are about the reference point; move to the centroid, then to the origin.
        var offset = centroid - reference;
        var aboutCentroid = inertiaAboutOrigin - area * Vector2.Dot(offset, offset);
        inertiaAboutOrigin = aboutCentroid + area * Vector2.Dot(centroid, centroid);
    }

    /// <summary>Distance of the farthest vertex from the origin.</summary>
    public readonly float Radius()
    {
        var radius = 0f;
        for (var i = 0; i < Count; i++)
            radius = MathF.Max(radius, Points[i].Length());
        return radius;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    internal static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>
    /// Writes the convex hull of <paramref name="points"/> into <paramref name="hull"/> (at least twice as long as the
    /// points) in this type's winding, and returns its corner count. Sorts <paramref name="points"/>' copy in place, so
    /// no allocation for up to 64 points.
    /// </summary>
    internal static int HullInto(ReadOnlySpan<Vector2> points, Span<Vector2> hull)
    {
        Span<Vector2> sorted = points.Length <= 64 ? stackalloc Vector2[points.Length] : new Vector2[points.Length];
        points.CopyTo(sorted);
        InsertionSort(sorted);

        // Andrew's monotone chain: lower hull left to right, then upper hull right to left.
        var count = 0;
        for (var pass = 0; pass < 2; pass++)
        {
            var start = count;
            for (var i = 0; i < sorted.Length; i++)
            {
                var point = pass == 0 ? sorted[i] : sorted[sorted.Length - 1 - i];
                while (count >= start + 2 && Cross(hull[count - 1] - hull[count - 2], point - hull[count - 1]) <= 1e-6f)
                    count--;
                hull[count++] = point;
            }

            count--; // The last point of each chain is the first of the other.
        }

        return Math.Max(count, 0);
    }

    /// <summary>By x, then y. The point sets are small, so no delegate and no allocation.</summary>
    private static void InsertionSort(Span<Vector2> points)
    {
        for (var i = 1; i < points.Length; i++)
        {
            var point = points[i];
            var j = i - 1;
            while (j >= 0 && (points[j].X > point.X || (points[j].X == point.X && points[j].Y > point.Y)))
            {
                points[j + 1] = points[j];
                j--;
            }

            points[j + 1] = point;
        }
    }

    internal static Vector2 Rotate(Vector2 v, float cos, float sin) => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

    private void ComputeNormals()
    {
        for (var i = 0; i < Count; i++)
        {
            var edge = Points[i + 1 < Count ? i + 1 : 0] - Points[i];
            Normals[i] = Vector2.Normalize(new Vector2(edge.Y, -edge.X));
        }
    }

    [InlineArray(MaxVertices)]
    internal struct Vertices
    {
        private Vector2 _element;
    }
}
