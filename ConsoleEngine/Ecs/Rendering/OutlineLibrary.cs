using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Rendering;

/// <summary>
/// Polygon outlines shared by many entities, referenced by <see cref="Components.Outline.Id"/>. Fill it once at
/// startup: adding allocates.
/// </summary>
public sealed class OutlineLibrary
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly List<Vector2[]> _vertices = new();
    private readonly List<float> _radii = new();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Adds a closed outline (the last vertex connects back to the first), in cells around the entity's position.
    /// Returns its id.
    /// </summary>
    public int Add(Vector2[] vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentOutOfRangeException.ThrowIfZero(vertices.Length);

        var radius = 0f;
        for (var i = 0; i < vertices.Length; i++)
            radius = MathF.Max(radius, vertices[i].Length());

        _vertices.Add(vertices);
        _radii.Add(radius);
        return _vertices.Count - 1;
    }

    public ReadOnlySpan<Vector2> GetVertices(int id) => _vertices[id];

    /// <summary>
    /// Distance from the origin to the farthest vertex. The same at every rotation, so it's used for culling.
    /// </summary>
    public float GetRadius(int id) => _radii[id];
}
