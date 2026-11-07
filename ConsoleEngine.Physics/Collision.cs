using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>A body's shape in the world for the narrowphase: where it is, how it is turned, and what it is.</summary>
internal struct BodyShape
{
    /// <summary>The body's origin (its <c>Position</c>), which it rotates around.</summary>
    public Vector2 Position;

    public float Cos;
    public float Sin;
    public ShapeType Shape;

    /// <summary>Circle radius.</summary>
    public float Radius;

    /// <summary>Local centre of a circle or box.</summary>
    public Vector2 Offset;

    public Vector2 HalfExtents;
    public int PolygonId;

    public readonly bool IsRotated => Sin != 0f || Cos != 1f;

    /// <summary>A shape that isn't turned (cos 1, sin 0).</summary>
    public static BodyShape Unrotated(Vector2 position, ShapeType shape) =>
        new() { Position = position, Cos = 1f, Shape = shape };

    public readonly Vector2 ToWorld(Vector2 local) =>
        Sin == 0f && Cos == 1f ? Position + local : Position + ConvexPolygon.Rotate(local, Cos, Sin);

    /// <summary>The shape as a world-space polygon. Boxes and polygons only.</summary>
    public readonly void ToWorldPolygon(PolygonLibrary polygons, out ConvexPolygon polygon)
    {
        if (Shape == ShapeType.Box)
        {
            var local = ConvexPolygon.Box(Offset, HalfExtents);
            polygon = local.Transform(Position, Cos, Sin);
        }
        else
        {
            polygon = polygons.Get(PolygonId).Transform(Position, Cos, Sin);
        }
    }
}

/// <summary>One contact point: halfway between the two surfaces, in world space.</summary>
internal struct ManifoldPoint
{
    public Vector2 Point;

    /// <summary>Negative when overlapping.</summary>
    public float Separation;

    /// <summary>Which features (edge, vertex) made the point, to match it with the last step's for warm starting.</summary>
    public int Id;
}

/// <summary>The contact between two shapes: a normal from A to B and up to two points (Box2D's manifold).</summary>
internal struct Manifold
{
    public Vector2 Normal;
    public int PointCount;
    public ManifoldPoint Point0;
    public ManifoldPoint Point1;

    /// <summary>The smaller separation of the points; <see cref="float.MaxValue"/> without points.</summary>
    public readonly float MinSeparation => PointCount == 0 ? float.MaxValue
        : PointCount == 1 ? Point0.Separation : MathF.Min(Point0.Separation, Point1.Separation);
}

/// <summary>
/// Narrowphase: the contact manifold of two shapes, as Box2D. Points are kept while their separation is at most
/// <c>speculative</c>, so the solver also gets contacts that are about to happen. Circles give one point; polygons
/// (boxes are polygons) up to two, from the separating axis test and clipping the incident edge against the reference
/// face (Box2D's <c>b2CollidePolygons</c>).
/// </summary>
internal static class Collision
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Prefer the first shape's face unless the other's is clearly better, so the choice doesn't flicker.</summary>
    private const float FaceTolerance = .1f * PhysicsSystem.LinearSlop;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static void Collide(in BodyShape a, in BodyShape b, PolygonLibrary polygons, float speculative,
        out Manifold manifold)
    {
        if (a.Shape == ShapeType.Circle)
        {
            var centerA = a.ToWorld(a.Offset);
            if (b.Shape == ShapeType.Circle)
            {
                CircleCircle(centerA, a.Radius, b.ToWorld(b.Offset), b.Radius, speculative, out manifold);
                return;
            }

            b.ToWorldPolygon(polygons, out var polygonB);
            PolygonCircle(polygonB, centerA, a.Radius, speculative, out manifold);
            manifold.Normal = -manifold.Normal;
            return;
        }

        a.ToWorldPolygon(polygons, out var polygonA);
        if (b.Shape == ShapeType.Circle)
        {
            PolygonCircle(polygonA, b.ToWorld(b.Offset), b.Radius, speculative, out manifold);
            return;
        }

        b.ToWorldPolygon(polygons, out var polygonOfB);
        Polygons(polygonA, polygonOfB, speculative, out manifold);
    }

    public static void CircleCircle(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB, float speculative,
        out Manifold manifold)
    {
        manifold = default;
        var offset = centerB - centerA;
        var distance = offset.Length();
        var separation = distance - radiusA - radiusB;
        if (separation > speculative)
            return;

        // Same centre: any direction separates them. Down is as good as any.
        var normal = distance > 1e-6f ? offset / distance : Vector2.UnitY;
        manifold.Normal = normal;
        manifold.PointCount = 1;
        manifold.Point0 = new ManifoldPoint
        {
            Point = .5f * (centerA + normal * radiusA + centerB - normal * radiusB),
            Separation = separation
        };
    }

    /// <summary>The normal points from the polygon to the circle.</summary>
    public static void PolygonCircle(in ConvexPolygon polygon, Vector2 center, float radius, float speculative,
        out Manifold manifold)
    {
        manifold = default;
        var face = 0;
        var faceSeparation = float.MinValue;
        for (var i = 0; i < polygon.Count; i++)
        {
            var s = Vector2.Dot(polygon.Normals[i], center - polygon.Points[i]);
            if (s > radius + speculative)
                return;

            if (s > faceSeparation)
            {
                faceSeparation = s;
                face = i;
            }
        }

        var v1 = polygon.Points[face];
        var v2 = polygon.Points[face + 1 < polygon.Count ? face + 1 : 0];
        Vector2 normal;
        float separation;
        Vector2 surface;

        if (faceSeparation > 1e-6f && Vector2.Dot(center - v1, v2 - v1) <= 0f)
        {
            if (!Corner(v1, center, radius, speculative, out normal, out separation))
                return;
            surface = v1;
        }
        else if (faceSeparation > 1e-6f && Vector2.Dot(center - v2, v1 - v2) <= 0f)
        {
            if (!Corner(v2, center, radius, speculative, out normal, out separation))
                return;
            surface = v2;
        }
        else
        {
            normal = polygon.Normals[face];
            separation = faceSeparation - radius;
            surface = center - normal * faceSeparation;
        }

        manifold.Normal = normal;
        manifold.PointCount = 1;
        manifold.Point0 = new ManifoldPoint
        {
            Point = .5f * (surface + center - normal * radius),
            Separation = separation,
            Id = face
        };
    }

    /// <summary>Box2D's polygon manifold: separating axis test, then the incident edge clipped to the reference.</summary>
    public static void Polygons(in ConvexPolygon a, in ConvexPolygon b, float speculative, out Manifold manifold)
    {
        manifold = default;
        var separationA = MaxSeparation(a, b, out var edgeA);
        if (separationA > speculative)
            return;

        var separationB = MaxSeparation(b, a, out var edgeB);
        if (separationB > speculative)
            return;

        var flip = separationB > separationA + FaceTolerance;
        if (flip)
            Clip(b, a, edgeB, speculative, 1 << 8, ref manifold);
        else
            Clip(a, b, edgeA, speculative, 0, ref manifold);

        if (flip)
            manifold.Normal = -manifold.Normal;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static bool Corner(Vector2 corner, Vector2 center, float radius, float speculative, out Vector2 normal,
        out float separation)
    {
        var offset = center - corner;
        var distance = offset.Length();
        separation = distance - radius;
        normal = distance > 1e-6f ? offset / distance : Vector2.UnitY;
        return separation <= speculative;
    }

    /// <summary>The face of <paramref name="a"/> that separates the polygons most, and by how much.</summary>
    private static float MaxSeparation(in ConvexPolygon a, in ConvexPolygon b, out int edge)
    {
        edge = 0;
        var best = float.MinValue;
        for (var i = 0; i < a.Count; i++)
        {
            var normal = a.Normals[i];
            var origin = a.Points[i];
            var deepest = float.MaxValue;
            for (var j = 0; j < b.Count; j++)
                deepest = MathF.Min(deepest, Vector2.Dot(normal, b.Points[j] - origin));

            if (deepest > best)
            {
                best = deepest;
                edge = i;
            }
        }

        return best;
    }

    /// <summary>
    /// Clips the edge of <paramref name="incident"/> that faces the reference face to that face's side planes, and keeps
    /// the points within <paramref name="speculative"/> of it.
    /// </summary>
    private static void Clip(in ConvexPolygon reference, in ConvexPolygon incident, int edge, float speculative,
        int idFlag, ref Manifold manifold)
    {
        var normal = reference.Normals[edge];

        // The incident edge is the one whose normal points most against the reference normal.
        var incidentEdge = 0;
        var minDot = float.MaxValue;
        for (var i = 0; i < incident.Count; i++)
        {
            var dot = Vector2.Dot(normal, incident.Normals[i]);
            if (dot < minDot)
            {
                minDot = dot;
                incidentEdge = i;
            }
        }

        var i2 = incidentEdge + 1 < incident.Count ? incidentEdge + 1 : 0;
        var p0 = incident.Points[incidentEdge];
        var p1 = incident.Points[i2];
        var id0 = idFlag | edge << 4 | incidentEdge;
        var id1 = idFlag | edge << 4 | i2;

        var v11 = reference.Points[edge];
        var v12 = reference.Points[edge + 1 < reference.Count ? edge + 1 : 0];
        var tangent = Vector2.Normalize(v12 - v11);

        if (!ClipToPlane(ref p0, ref p1, ref id0, ref id1, -tangent, -Vector2.Dot(tangent, v11), 1 << 9) ||
            !ClipToPlane(ref p0, ref p1, ref id0, ref id1, tangent, Vector2.Dot(tangent, v12), 1 << 10))
            return;

        var faceOffset = Vector2.Dot(normal, v11);
        manifold.Normal = normal;
        AddClipped(ref manifold, p0, Vector2.Dot(normal, p0) - faceOffset, normal, id0, speculative);
        AddClipped(ref manifold, p1, Vector2.Dot(normal, p1) - faceOffset, normal, id1, speculative);
    }

    private static void AddClipped(ref Manifold manifold, Vector2 point, float separation, Vector2 normal, int id,
        float speculative)
    {
        if (separation > speculative)
            return;

        // Halfway between the incident point and the reference face.
        var contact = new ManifoldPoint { Point = point - normal * (.5f * separation), Separation = separation, Id = id };
        if (manifold.PointCount == 0)
            manifold.Point0 = contact;
        else
            manifold.Point1 = contact;
        manifold.PointCount++;
    }

    /// <summary>
    /// Keeps the part of the segment where <c>dot(planeNormal, p) &lt;= offset</c> (Box2D's <c>b2ClipSegmentToLine</c>).
    /// False if nothing is left.
    /// </summary>
    private static bool ClipToPlane(ref Vector2 p0, ref Vector2 p1, ref int id0, ref int id1, Vector2 planeNormal,
        float offset, int clipFlag)
    {
        var d0 = Vector2.Dot(planeNormal, p0) - offset;
        var d1 = Vector2.Dot(planeNormal, p1) - offset;
        if (d0 > 0f && d1 > 0f)
            return false;

        if (d0 > 0f)
        {
            p0 += (p1 - p0) * (d0 / (d0 - d1));
            id0 |= clipFlag;
        }
        else if (d1 > 0f)
        {
            p1 += (p0 - p1) * (d1 / (d1 - d0));
            id1 |= clipFlag;
        }

        return true;
    }
}
