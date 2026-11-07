using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Ray tests for the spatial queries. A ray is <c>origin + fraction * translation</c> with fraction 0 to 1. A box cast
/// is a ray from the box's centre against each shape grown by the box (Minkowski sum): a box grows into a bigger box, a
/// circle into a box with rounded corners.
/// <para>
/// Starting inside a shape: a plain ray ignores it (as Box2D). A cast (<paramref name="castFromInside"/>) hits it at
/// fraction 0 if it moves further in, and ignores it if it moves out, so a body that ends up slightly inside a wall can
/// still leave it.
/// </para>
/// </summary>
internal static class RayCast
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Ray against the axis-aligned box <paramref name="center"/> ± <paramref name="extents"/>.</summary>
    public static bool Box(Vector2 origin, Vector2 translation, Vector2 center, Vector2 extents, bool castFromInside,
        out float fraction, out Vector2 normal)
    {
        fraction = 0f;
        normal = default;
        var min = center - extents;
        var max = center + extents;
        var enter = float.NegativeInfinity;
        var exit = float.PositiveInfinity;
        var enterNormal = Vector2.Zero;

        if (!Slab(origin.X, translation.X, min.X, max.X, ref enter, ref exit, ref enterNormal, Vector2.UnitX) ||
            !Slab(origin.Y, translation.Y, min.Y, max.Y, ref enter, ref exit, ref enterNormal, Vector2.UnitY))
            return false;

        if (enter > exit || exit < 0f || enter > 1f)
            return false;

        if (enter >= 0f)
        {
            fraction = enter;
            normal = enterNormal;
            return true;
        }

        // The origin is inside the box.
        if (!castFromInside)
            return false;

        var offset = origin - center;
        var depthX = extents.X - MathF.Abs(offset.X);
        var depthY = extents.Y - MathF.Abs(offset.Y);
        normal = depthX < depthY ? new Vector2(SignOrPositive(offset.X), 0f) : new Vector2(0f, SignOrPositive(offset.Y));
        return Vector2.Dot(translation, normal) < 0f;
    }

    /// <summary>Ray against a circle.</summary>
    public static bool Circle(Vector2 origin, Vector2 translation, Vector2 center, float radius, bool castFromInside,
        out float fraction, out Vector2 normal)
    {
        fraction = 0f;
        normal = default;
        var offset = origin - center;
        var c = offset.LengthSquared() - radius * radius;

        if (c < 0f)
        {
            if (!castFromInside)
                return false;

            var length = offset.Length();
            normal = length > 1e-6f ? offset / length : Vector2.UnitY;
            return Vector2.Dot(translation, normal) < 0f;
        }

        // |offset + t * translation|² = r²: a t² + 2 b t + c = 0, entry is the smaller root.
        var a = translation.LengthSquared();
        var b = Vector2.Dot(offset, translation);
        if (a < 1e-12f || b >= 0f)
            return false;

        var discriminant = b * b - a * c;
        if (discriminant < 0f)
            return false;

        var t = (-b - MathF.Sqrt(discriminant)) / a;
        if (t > 1f)
            return false;

        fraction = MathF.Max(t, 0f);
        normal = Vector2.Normalize(offset + translation * fraction);
        return true;
    }

    /// <summary>
    /// Ray against a box with rounded corners: <paramref name="center"/> ± <paramref name="extents"/>, grown by
    /// <paramref name="radius"/>. That's a box cast against a circle (extents = the cast box's half size).
    /// </summary>
    public static bool RoundedBox(Vector2 origin, Vector2 translation, Vector2 center, Vector2 extents, float radius,
        bool castFromInside, out float fraction, out Vector2 normal)
    {
        var grown = extents + new Vector2(radius, radius);
        if (!Box(origin, translation, center, grown, castFromInside, out fraction, out normal))
            return false;

        // Inside the corner region of the grown box, the surface is a quarter circle around the box's corner.
        var hit = origin + translation * fraction - center;
        if (MathF.Abs(hit.X) <= extents.X || MathF.Abs(hit.Y) <= extents.Y)
            return true;

        var corner = center + new Vector2(SignOrPositive(hit.X) * extents.X, SignOrPositive(hit.Y) * extents.Y);
        return Circle(origin, translation, corner, radius, castFromInside, out fraction, out normal);
    }

    /// <summary>
    /// Ray against a convex polygon given by its corners in <see cref="ConvexPolygon"/> winding (Box2D's
    /// <c>b2RayCastPolygon</c>: clip the ray against every edge's half-plane).
    /// </summary>
    public static bool Polygon(Vector2 origin, Vector2 translation, ReadOnlySpan<Vector2> points, bool castFromInside,
        out float fraction, out Vector2 normal)
    {
        fraction = 0f;
        normal = default;
        var lower = 0f;
        var upper = 1f;
        var entering = -1;
        var enterNormal = Vector2.Zero;
        var deepest = float.MinValue;
        var deepestNormal = Vector2.Zero;

        for (var i = 0; i < points.Length; i++)
        {
            var edge = points[i + 1 < points.Length ? i + 1 : 0] - points[i];
            var edgeNormal = Vector2.Normalize(new Vector2(edge.Y, -edge.X));
            var numerator = Vector2.Dot(edgeNormal, points[i] - origin);
            var denominator = Vector2.Dot(edgeNormal, translation);

            // How far outside this edge the origin is; the largest one is the nearest way out from inside.
            if (-numerator > deepest)
            {
                deepest = -numerator;
                deepestNormal = edgeNormal;
            }

            if (denominator == 0f)
            {
                if (numerator < 0f)
                    return false;
            }
            else if (denominator < 0f && numerator < lower * denominator)
            {
                lower = numerator / denominator;
                entering = i;
                enterNormal = edgeNormal;
            }
            else if (denominator > 0f && numerator < upper * denominator)
            {
                upper = numerator / denominator;
            }

            if (upper < lower)
                return false;
        }

        if (entering >= 0)
        {
            fraction = lower;
            normal = enterNormal;
            return true;
        }

        // The origin is inside the polygon.
        if (!castFromInside)
            return false;

        normal = deepestNormal;
        return Vector2.Dot(translation, normal) < 0f;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Narrows [enter, exit] to the fractions where the ray is between min and max on one axis.</summary>
    private static bool Slab(float origin, float direction, float min, float max, ref float enter, ref float exit,
        ref Vector2 enterNormal, Vector2 axis)
    {
        if (MathF.Abs(direction) < 1e-12f)
            return origin >= min && origin <= max;

        var inverse = 1f / direction;
        var near = (min - origin) * inverse;
        var far = (max - origin) * inverse;
        var normal = -axis;
        if (near > far)
        {
            (near, far) = (far, near);
            normal = axis;
        }

        if (near > enter)
        {
            enter = near;
            enterNormal = normal;
        }

        exit = MathF.Min(exit, far);
        return true;
    }

    private static float SignOrPositive(float value) => value < 0f ? -1f : 1f;
}
