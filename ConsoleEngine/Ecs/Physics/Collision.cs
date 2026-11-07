using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// Narrowphase: exact shape tests for circles and axis-aligned boxes. Each test gives one contact: a unit
/// <c>normal</c> pointing from A to B and the <c>separation</c> along it, which is negative when the shapes overlap
/// (its size is the depth). Shapes that only touch count as colliding with separation 0.
/// </summary>
internal static class Collision
{
    //**********************************************************
    //** public methods
    //**********************************************************

    public static bool Collide(in BodyProxy a, in BodyProxy b, out Vector2 normal, out float separation)
    {
        if (a.Shape == ShapeType.Circle)
        {
            return b.Shape == ShapeType.Circle
                ? CircleCircle(a.Center, a.Radius, b.Center, b.Radius, out normal, out separation)
                : CircleBox(a.Center, a.Radius, b.Center, b.Extents, out normal, out separation);
        }

        if (b.Shape == ShapeType.Box)
            return BoxBox(a.Center, a.Extents, b.Center, b.Extents, out normal, out separation);

        var hit = CircleBox(b.Center, b.Radius, a.Center, a.Extents, out normal, out separation);
        normal = -normal;
        return hit;
    }

    public static bool CircleCircle(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB,
        out Vector2 normal, out float separation)
    {
        var offset = centerB - centerA;
        var reach = radiusA + radiusB;
        var distanceSquared = offset.LengthSquared();
        if (distanceSquared > reach * reach)
        {
            normal = default;
            separation = default;
            return false;
        }

        var distance = MathF.Sqrt(distanceSquared);

        // Same centre: any direction separates them. Down is as good as any.
        normal = distance > 1e-6f ? offset / distance : Vector2.UnitY;
        separation = distance - reach;
        return true;
    }

    public static bool BoxBox(Vector2 centerA, Vector2 extentsA, Vector2 centerB, Vector2 extentsB,
        out Vector2 normal, out float separation)
    {
        var offset = centerB - centerA;
        var overlapX = extentsA.X + extentsB.X - MathF.Abs(offset.X);
        var overlapY = extentsA.Y + extentsB.Y - MathF.Abs(offset.Y);
        if (overlapX < 0f || overlapY < 0f)
        {
            normal = default;
            separation = default;
            return false;
        }

        // Push out along the axis of least overlap.
        if (overlapX < overlapY)
        {
            normal = new Vector2(SignOrPositive(offset.X), 0f);
            separation = -overlapX;
        }
        else
        {
            normal = new Vector2(0f, SignOrPositive(offset.Y));
            separation = -overlapY;
        }

        return true;
    }

    /// <summary>The normal points from the circle to the box.</summary>
    public static bool CircleBox(Vector2 circleCenter, float radius, Vector2 boxCenter, Vector2 boxExtents,
        out Vector2 normal, out float separation)
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
                separation = -(depthX + radius);
            }
            else
            {
                normal = new Vector2(0f, -SignOrPositive(offset.Y));
                separation = -(depthY + radius);
            }

            return true;
        }

        var outside = offset - closest;
        var distanceSquared = outside.LengthSquared();
        if (distanceSquared > radius * radius)
        {
            normal = default;
            separation = default;
            return false;
        }

        var distance = MathF.Sqrt(distanceSquared);
        normal = -outside / distance;
        separation = distance - radius;
        return true;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float SignOrPositive(float value) => value < 0f ? -1f : 1f;
}
