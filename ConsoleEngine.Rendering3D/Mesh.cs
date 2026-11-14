using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// Triangles with one flat colour each. Counter-clockwise triangles (seen from outside) face outwards, the OBJ
/// convention. Normals are per triangle and in model space.
/// </summary>
public sealed class Mesh
{
    //**********************************************************
    //** ctor
    //**********************************************************

    public Mesh(Vector3[] positions, int[] indices, Vector3[] triangleColors)
    {
        Positions = positions;
        Indices = indices;
        TriangleColors = triangleColors;
        TriangleNormals = new Vector3[indices.Length / 3];

        for (var t = 0; t < TriangleNormals.Length; t++)
        {
            var a = positions[indices[t * 3]];
            var b = positions[indices[t * 3 + 1]];
            var c = positions[indices[t * 3 + 2]];
            var normal = Vector3.Cross(b - a, c - a);
            TriangleNormals[t] = normal == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(normal);
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    public Vector3[] Positions { get; }
    public int[] Indices { get; }
    public Vector3[] TriangleColors { get; }
    public Vector3[] TriangleNormals { get; }
    public int TriangleCount => TriangleNormals.Length;

    /// <summary>
    /// 0..1 per triangle, drawn as screen-door transparency in palette mode and alpha-blended in true colour (see
    /// <see cref="Rasterizer"/>). Blended triangles write no depth, so draw see-through meshes after the opaque ones.
    /// Null: every triangle is opaque.
    /// </summary>
    public float[] TriangleOpacity { get; init; }

    /// <summary>Drawn in <see cref="TriangleColors"/> as they are, without the light (smoke, glowing things).</summary>
    public bool IsUnlit { get; init; }

    /// <summary>
    /// Bumped by <see cref="MarkChanged"/>. A renderer that keeps its own copy of the mesh (a GPU buffer) copies it
    /// again when this has changed.
    /// </summary>
    public int Version { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Call after changing the arrays (positions, colours, opacity) in place.</summary>
    public void MarkChanged() => Version++;

    /// <summary>A flat chequered square on y = 0, centred on the origin, facing up.</summary>
    public static Mesh CreateFloor(float size, int tiles, Vector3 color, Vector3 otherColor)
    {
        var positions = new Vector3[tiles * tiles * 4];
        var indices = new int[tiles * tiles * 6];
        var colors = new Vector3[tiles * tiles * 2];
        var tileSize = size / tiles;
        var start = -size / 2;

        for (var z = 0; z < tiles; z++)
        for (var x = 0; x < tiles; x++)
        {
            var tile = z * tiles + x;
            var v = tile * 4;
            var x0 = start + x * tileSize;
            var z0 = start + z * tileSize;
            positions[v] = new Vector3(x0, 0, z0);
            positions[v + 1] = new Vector3(x0, 0, z0 + tileSize);
            positions[v + 2] = new Vector3(x0 + tileSize, 0, z0 + tileSize);
            positions[v + 3] = new Vector3(x0 + tileSize, 0, z0);
            indices[tile * 6] = v;
            indices[tile * 6 + 1] = v + 1;
            indices[tile * 6 + 2] = v + 2;
            indices[tile * 6 + 3] = v;
            indices[tile * 6 + 4] = v + 2;
            indices[tile * 6 + 5] = v + 3;
            colors[tile * 2] = colors[tile * 2 + 1] = (x + z) % 2 == 0 ? color : otherColor;
        }

        return new Mesh(positions, indices, colors);
    }
}
