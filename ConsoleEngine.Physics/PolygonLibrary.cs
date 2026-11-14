using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// The convex polygon shapes of a <see cref="PhysicsSystem"/>, shared by id like <c>OutlineLibrary</c> outlines: a
/// <see cref="Collider"/> made with <see cref="Collider.Polygon"/> holds only the id, so components stay small. Add
/// shapes while setting up a scene, not per frame.
/// </summary>
public sealed class PolygonLibrary
{
    //**********************************************************
    //** fields
    //**********************************************************

    private ConvexPolygon[] _polygons = new ConvexPolygon[8];
    private PolygonMass[] _masses = new PolygonMass[8];

    //**********************************************************
    //** props
    //**********************************************************

    public int Count { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Adds the convex hull of <paramref name="vertices"/> (relative to the body's <c>Position</c>, which is also the
    /// point it rotates around) and returns its id. At most 8 corners after the hull is taken.
    /// </summary>
    public int Add(ReadOnlySpan<Vector2> vertices)
    {
        var polygon = ConvexPolygon.Hull(vertices);
        polygon.ComputeMass(out var area, out _, out var inertia);
        if (Count == _polygons.Length)
        {
            Array.Resize(ref _polygons, Count * 2);
            Array.Resize(ref _masses, Count * 2);
        }

        _polygons[Count] = polygon;
        _masses[Count] = new PolygonMass(inertia / area, polygon.Radius());
        return Count++;
    }

    /// <summary>The hull's corners, in order. For drawing the same shape, e.g. with an <c>OutlineLibrary</c>.</summary>
    public Vector2[] GetVertices(int id)
    {
        ref readonly var polygon = ref Get(id);
        var vertices = new Vector2[polygon.Count];
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = polygon.Points[i];
        return vertices;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    internal ref readonly ConvexPolygon Get(int id)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, (uint)Count, nameof(id));
        return ref _polygons[id];
    }

    /// <summary>Rotational inertia about the body origin per unit of mass, for a body of uniform density.</summary>
    internal float InertiaPerMass(int id) => _masses[id].InertiaPerMass;

    /// <summary>Distance of the farthest corner from the body origin.</summary>
    internal float Radius(int id) => _masses[id].Radius;

    private readonly struct PolygonMass(float inertiaPerMass, float radius)
    {
        public readonly float InertiaPerMass = inertiaPerMass;
        public readonly float Radius = radius;
    }
}
