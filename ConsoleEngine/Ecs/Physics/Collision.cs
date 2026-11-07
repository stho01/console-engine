using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// Narrowphase: exact shape tests for circles and axis-aligned boxes. Each test gives one contact: a unit
/// <c>normal</c> pointing from A to B, and the <c>separation</c> along it (the return value). Separation is negative
/// when the shapes overlap (its size is the depth), 0 when they touch, and the gap when they are apart, so the solver
/// can also use contacts that are about to happen (speculative contacts). For boxes that are apart diagonally the gap is
/// the larger of the two axis gaps (separating axis test), which is never more than the real distance.
/// </summary>
internal static class Collision
{
    //**********************************************************
    //** public methods
    //**********************************************************

    public static float Collide(in BodyProxy a, in BodyProxy b, out Vector2 normal)
    {
        if (a.Shape == ShapeType.Circle)
        {
            return b.Shape == ShapeType.Circle
                ? CircleCircle(a.Center, a.Radius, b.Center, b.Radius, out normal)
                : CircleBox(a.Center, a.Radius, b.Center, b.Extents, out normal);
        }

        if (b.Shape == ShapeType.Box)
            return BoxBox(a.Center, a.Extents, b.Center, b.Extents, out normal);

        var separation = CircleBox(b.Center, b.Radius, a.Center, a.Extents, out normal);
        normal = -normal;
        return separation;
    }

    public static float CircleCircle(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB, out Vector2 normal)
    {
        var offset = centerB - centerA;
        var distance = offset.Length();

        // Same centre: any direction separates them. Down is as good as any.
        normal = distance > 1e-6f ? offset / distance : Vector2.UnitY;
        return distance - radiusA - radiusB;
    }

    public static float BoxBox(Vector2 centerA, Vector2 extentsA, Vector2 centerB, Vector2 extentsB, out Vector2 normal)
    {
        var offset = centerB - centerA;
        var gapX = MathF.Abs(offset.X) - extentsA.X - extentsB.X;
        var gapY = MathF.Abs(offset.Y) - extentsA.Y - extentsB.Y;

        // The axis with the larger gap: the separating axis when apart, the axis of least overlap when overlapping.
        if (gapX > gapY)
        {
            normal = new Vector2(SignOrPositive(offset.X), 0f);
            return gapX;
        }

        normal = new Vector2(0f, SignOrPositive(offset.Y));
        return gapY;
    }

    /// <summary>The normal points from the circle to the box.</summary>
    public static float CircleBox(Vector2 circleCenter, float radius, Vector2 boxCenter, Vector2 boxExtents,
        out Vector2 normal)
    {
        var offset = circleCenter - boxCenter;
        var closest = Vector2.Clamp(offset, -boxExtents, boxExtents);

        if (closest == offset)
        {
            // The centre is inside the box: leave through the nearest side.
            var depthX = boxExtents.X - MathF.Abs(offset.X);
            var depthY = boxExtents.Y - MathF.Abs(offset.Y);
            if (depthX < depthY)
            {
                normal = new Vector2(-SignOrPositive(offset.X), 0f);
                return -(depthX + radius);
            }

            normal = new Vector2(0f, -SignOrPositive(offset.Y));
            return -(depthY + radius);
        }

        var outside = offset - closest;
        var distance = outside.Length();
        normal = -outside / distance;
        return distance - radius;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float SignOrPositive(float value) => value < 0f ? -1f : 1f;
}
