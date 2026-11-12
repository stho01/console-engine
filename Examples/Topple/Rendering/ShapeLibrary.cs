using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Topple.Rendering;

/// <summary>
/// A convex shape for filled drawing, in the body's local space (around its position, before rotation). Knows how far
/// a point is inside it, so the renderer can fill cells and pick border or texture characters.
/// </summary>
public sealed class Shape
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Vector2[] _normals;
    private readonly float[] _offsets;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="vertices">Convex, in the same winding as the physics' polygons.</param>
    /// <param name="isRoof">Drawn with roof tiles instead of the material's texture.</param>
    public Shape(Vector2[] vertices, bool isRoof)
    {
        IsRoof = isRoof;
        _normals = new Vector2[vertices.Length];
        _offsets = new float[vertices.Length];
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        for (var i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Length];
            var edge = b - a;
            var normal = Vector2.Normalize(new Vector2(edge.Y, -edge.X));
            _normals[i] = normal;
            _offsets[i] = Vector2.Dot(normal, a);
            min = Vector2.Min(min, a);
            max = Vector2.Max(max, a);
            Radius = MathF.Max(Radius, a.Length());
        }

        // Pick the winding that makes the normals point outwards: the centre must be inside.
        if (EdgeDistance(Vector2.Zero) < 0f)
        {
            for (var i = 0; i < _normals.Length; i++)
            {
                _normals[i] = -_normals[i];
                _offsets[i] = -_offsets[i];
            }
        }

        Min = min;
        Size = max - min;
        GrainAngle = Size.Y > Size.X ? MathF.PI / 2f : 0f;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Distance of the farthest corner from the origin.</summary>
    public float Radius { get; }

    /// <summary>The lower corner of the local bounding box; texture coordinates start here.</summary>
    public Vector2 Min { get; }

    public Vector2 Size { get; }

    /// <summary>Local direction of the long side: 0 along x, π/2 along y. Wood grain and stone joints follow it.</summary>
    public float GrainAngle { get; }

    /// <summary>At most one cell thick, so it has no room for a border.</summary>
    public bool IsThin => MathF.Min(Size.X, Size.Y) <= 1.01f;

    public bool IsRoof { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>How far a local point is inside the shape (negative outside).</summary>
    public float EdgeDistance(Vector2 local)
    {
        var distance = float.MaxValue;
        for (var i = 0; i < _normals.Length; i++)
            distance = MathF.Min(distance, _offsets[i] - Vector2.Dot(_normals[i], local));
        return distance;
    }
}

/// <summary>The shapes of the drawn bodies. Components can't hold arrays, so <c>BlockLook</c> stores an id.</summary>
public sealed class ShapeLibrary
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly List<Shape> _shapes = [];
    private readonly Dictionary<Vector2, int> _boxes = [];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>A box of the half size; boxes of the same size share one id.</summary>
    public int AddBox(Vector2 halfSize)
    {
        if (_boxes.TryGetValue(halfSize, out var id))
            return id;

        id = Add(new Shape([new(-halfSize.X, -halfSize.Y), new(halfSize.X, -halfSize.Y), new(halfSize.X, halfSize.Y),
            new(-halfSize.X, halfSize.Y)], isRoof: false));
        _boxes.Add(halfSize, id);
        return id;
    }

    public int Add(Shape shape)
    {
        _shapes.Add(shape);
        return _shapes.Count - 1;
    }

    public Shape Get(int id) => _shapes[id];
}
