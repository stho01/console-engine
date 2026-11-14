using System;
using System.Numerics;

namespace KartViewer.Rendering;

/// <summary>
/// Software triangle rasterizer: transforms a <see cref="Mesh"/>, clips it at the near plane, culls back faces,
/// lights each triangle flat
/// (ambient + one directional light) and fills it into <see cref="Colors"/> with a depth test. Allocates nothing per
/// frame once its vertex buffer has grown to the largest mesh.
/// </summary>
public sealed class Rasterizer
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float Ambient = 0.35f;
    private const float Diffuse = 0.65f;

    private readonly ConsolePalette _palette;
    private readonly float[] _depth;
    private float _opacity = 1f; // of the triangle being filled

    /// <summary>
    /// Screen-door transparency: a pixel of a triangle with opacity <c>o</c> is drawn where this 4×4 ordered
    /// threshold is below <c>o</c>, so <c>o</c> of the pixels show and the rest let the scene behind through.
    /// </summary>
    private static readonly float[] ScreenDoor = BuildScreenDoor();
    private Vector4[] _clip = [];
    private Vector3[] _screen = [];

    //**********************************************************
    //** ctor
    //**********************************************************

    public Rasterizer(int width, int height, ConsolePalette palette)
    {
        _palette = palette;
        Width = width;
        Height = height;
        Colors = new ConsoleColor[width * height];
        _depth = new float[width * height];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }

    /// <summary>The frame, one colour per pixel, <c>index = y * Width + x</c>.</summary>
    public ConsoleColor[] Colors { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Starts a frame: copies the background into <see cref="Colors"/> and resets the depth buffer.</summary>
    public void Clear(ReadOnlySpan<ConsoleColor> background)
    {
        background.CopyTo(Colors);
        Array.Fill(_depth, 1f);
    }

    /// <param name="toLight">Unit vector from the scene towards the light, in world space.</param>
    public void Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection, Vector3 toLight)
    {
        var positions = mesh.Positions;
        if (_clip.Length < positions.Length)
        {
            _clip = new Vector4[positions.Length];
            _screen = new Vector3[positions.Length];
        }

        var worldViewProjection = world * viewProjection;
        for (var i = 0; i < positions.Length; i++)
        {
            _clip[i] = Vector4.Transform(positions[i], worldViewProjection);
            _screen[i] = ToScreen(_clip[i]); // garbage behind the near plane, those corners are clipped first
        }

        var indices = mesh.Indices;
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var ia = indices[t * 3];
            var ib = indices[t * 3 + 1];
            var ic = indices[t * 3 + 2];
            _opacity = mesh.TriangleOpacity == null ? 1f : mesh.TriangleOpacity[t];
            if (_opacity <= 0f)
                continue;

            var color = Shade(mesh, t, world, toLight);

            // z < 0 is in front of the near plane (System.Numerics projections map near..far to 0..1).
            var behind = (_clip[ia].Z < 0 ? 1 : 0) + (_clip[ib].Z < 0 ? 1 : 0) + (_clip[ic].Z < 0 ? 1 : 0);
            if (behind == 0)
                FillIfFrontFacing(_screen[ia], _screen[ib], _screen[ic], color);
            else if (behind < 3)
                ClipAndFill(_clip[ia], _clip[ib], _clip[ic], color);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static Vector3 Shade(Mesh mesh, int triangle, Matrix4x4 world, Vector3 toLight)
    {
        if (mesh.IsUnlit)
            return mesh.TriangleColors[triangle];

        var normal = Vector3.Normalize(Vector3.TransformNormal(mesh.TriangleNormals[triangle], world));
        return mesh.TriangleColors[triangle] * (Ambient + Diffuse * MathF.Max(0, Vector3.Dot(normal, toLight)));
    }

    private Vector3 ToScreen(Vector4 clip)
    {
        var inverseW = 1f / clip.W;
        return new Vector3(
            (clip.X * inverseW + 1f) * 0.5f * Width,
            (1f - clip.Y * inverseW) * 0.5f * Height,
            clip.Z * inverseW);
    }

    /// <summary>
    /// Cuts the part of the triangle behind the near plane (z &lt; 0) off. One or two corners behind leave a
    /// triangle or a quad, filled as one or two triangles. Corner order is kept, so the winding stays.
    /// </summary>
    private void ClipAndFill(Vector4 a, Vector4 b, Vector4 c, Vector3 color)
    {
        Span<Vector3> kept = stackalloc Vector3[4];
        var count = 0;
        ClipEdge(a, b, kept, ref count);
        ClipEdge(b, c, kept, ref count);
        ClipEdge(c, a, kept, ref count);

        for (var i = 2; i < count; i++)
            FillIfFrontFacing(kept[0], kept[i - 1], kept[i], color);
    }

    /// <summary>Keeps <paramref name="from"/> if in front, and where the edge crosses the near plane.</summary>
    private void ClipEdge(Vector4 from, Vector4 to, Span<Vector3> kept, ref int count)
    {
        if (from.Z >= 0)
            kept[count++] = ToScreen(from);
        if ((from.Z < 0) != (to.Z < 0))
            kept[count++] = ToScreen(Vector4.Lerp(from, to, from.Z / (from.Z - to.Z)));
    }

    private void FillIfFrontFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 color)
    {
        // Counter-clockwise front faces turn clockwise (negative area) once screen y points down.
        var area = Edge(a, b, c.X, c.Y);
        if (area < 0)
            Fill(a, b, c, area, color);
    }

    private static float[] BuildScreenDoor()
    {
        int[] bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        var thresholds = new float[bayer.Length];
        for (var i = 0; i < bayer.Length; i++)
            thresholds[i] = (bayer[i] + 0.5f) / bayer.Length;
        return thresholds;
    }

    /// <summary>Twice the signed area of the triangle a, b, p.</summary>
    private static float Edge(Vector3 a, Vector3 b, float px, float py) =>
        (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);

    private void Fill(Vector3 a, Vector3 b, Vector3 c, float area, Vector3 color)
    {
        var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        var maxX = Math.Min(Width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        var maxY = Math.Min(Height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        if (minX > maxX || minY > maxY)
            return;

        // Barycentric weights of a, b and c at the first pixel centre, and how much they change per pixel step.
        // Dividing by the (negative) area makes them positive inside the triangle.
        var inverseArea = 1f / area;
        var startX = minX + 0.5f;
        var startY = minY + 0.5f;
        var rowA = Edge(b, c, startX, startY) * inverseArea;
        var rowB = Edge(c, a, startX, startY) * inverseArea;
        var rowC = Edge(a, b, startX, startY) * inverseArea;
        var stepXA = (b.Y - c.Y) * inverseArea;
        var stepXB = (c.Y - a.Y) * inverseArea;
        var stepXC = (a.Y - b.Y) * inverseArea;
        var stepYA = (c.X - b.X) * inverseArea;
        var stepYB = (a.X - c.X) * inverseArea;
        var stepYC = (b.X - a.X) * inverseArea;

        for (var y = minY; y <= maxY; y++)
        {
            var weightA = rowA;
            var weightB = rowB;
            var weightC = rowC;
            var index = y * Width + minX;

            for (var x = minX; x <= maxX; x++, index++)
            {
                if (weightA >= 0 && weightB >= 0 && weightC >= 0)
                {
                    var depth = weightA * a.Z + weightB * b.Z + weightC * c.Z;
                    if (depth < _depth[index] && (_opacity >= 1f || ScreenDoor[((y & 3) << 2) | (x & 3)] < _opacity))
                    {
                        _depth[index] = depth;
                        Colors[index] = _palette.ToConsoleColor(color, x, y);
                    }
                }

                weightA += stepXA;
                weightB += stepXB;
                weightC += stepXC;
            }

            rowA += stepYA;
            rowB += stepYB;
            rowC += stepYC;
        }
    }
}
