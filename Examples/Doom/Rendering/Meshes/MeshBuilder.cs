using System;
using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Doom.Rendering.Meshes;

/// <summary>
/// Collects triangles (one colour each) in reused, growing arrays, then copies them into a <see cref="Mesh"/>:
/// into the old one when it is big enough (the rest padded with empty triangles, which draw nothing), else into a
/// new one with room to grow. So a sector that changes every tic (a door) stops allocating once its mesh is as big
/// as it gets. Cells that touch share their corners (each distinct position is one vertex), so the renderer
/// transforms fewer vertices. Positions are world space for <c>ConsoleEngine.Rendering3D</c>: x east, y up, z south.
/// </summary>
public sealed class MeshBuilder
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Vector3[] _positions = new Vector3[256];
    private int[] _indices = new int[384];
    private Vector3[] _colors = new Vector3[128];
    private readonly Dictionary<Vector3, int> _vertexIndex = [];

    //**********************************************************
    //** props
    //**********************************************************

    public int VertexCount { get; private set; }
    public int TriangleCount { get; private set; }

    /// <summary>The bounds of the vertices added since <see cref="Clear"/>.</summary>
    public Vector3 Min { get; private set; }

    public Vector3 Max { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Clear()
    {
        VertexCount = 0;
        TriangleCount = 0;
        _vertexIndex.Clear();
        Min = new Vector3(float.MaxValue);
        Max = new Vector3(float.MinValue);
    }

    /// <summary>A map point at a height: x east, y north, z up in Doom; x, y up, -y in the mesh.</summary>
    public static Vector3 ToWorld(float x, float y, float z) => new(x, z, -y);

    /// <summary>The index of the vertex at <paramref name="position"/>, added if there is none there yet.</summary>
    public int AddVertex(Vector3 position)
    {
        if (_vertexIndex.TryGetValue(position, out var existing))
            return existing;

        _vertexIndex.Add(position, VertexCount);
        if (VertexCount == _positions.Length)
            Array.Resize(ref _positions, _positions.Length * 2);

        Min = Vector3.Min(Min, position);
        Max = Vector3.Max(Max, position);
        _positions[VertexCount] = position;
        return VertexCount++;
    }

    /// <summary>Corners counter-clockwise as seen from the side that shows it.</summary>
    public void AddTriangle(int a, int b, int c, Vector3 color)
    {
        if (TriangleCount == _colors.Length)
        {
            Array.Resize(ref _colors, _colors.Length * 2);
            Array.Resize(ref _indices, _colors.Length * 3);
        }

        _indices[TriangleCount * 3] = a;
        _indices[TriangleCount * 3 + 1] = b;
        _indices[TriangleCount * 3 + 2] = c;
        _colors[TriangleCount++] = color;
    }

    /// <summary>A quad, corners counter-clockwise as seen from the side that shows it.</summary>
    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 color)
    {
        var ia = AddVertex(a);
        var ic = AddVertex(c);
        AddTriangle(ia, AddVertex(b), ic, color);
        AddTriangle(ia, ic, AddVertex(d), color);
    }

    /// <summary>
    /// The triangles as a mesh: <paramref name="current"/> refilled when it has room, else a new mesh (with room to
    /// grow when it replaces one); null when there are no triangles. <paramref name="unlit"/> applies to a new mesh.
    /// </summary>
    public Mesh ToMesh(Mesh current, bool unlit = false)
    {
        if (TriangleCount == 0)
            return null;

        if (current == null || current.Positions.Length < VertexCount || current.TriangleCount < TriangleCount)
        {
            var growth = current == null ? 1f : 1.5f;
            current = new Mesh(new Vector3[(int)(VertexCount * growth)], new int[(int)(TriangleCount * growth) * 3],
                new Vector3[(int)(TriangleCount * growth)]) { IsUnlit = unlit };
        }

        Array.Copy(_positions, current.Positions, VertexCount);
        Array.Copy(_indices, current.Indices, TriangleCount * 3);
        Array.Copy(_colors, current.TriangleColors, TriangleCount);
        Array.Fill(current.Positions, _positions[0], VertexCount, current.Positions.Length - VertexCount);
        Array.Fill(current.Indices, 0, TriangleCount * 3, current.Indices.Length - TriangleCount * 3);
        current.MarkChanged();
        return current;
    }
}
