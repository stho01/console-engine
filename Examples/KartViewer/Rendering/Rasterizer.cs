using System;
using System.Numerics;

namespace KartViewer.Rendering;

/// <summary>
/// Software triangle rasterizer: transforms a <see cref="Mesh"/>, culls back faces, lights each triangle flat
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

    private readonly float[] _depth;
    private Vector3[] _screen = [];

    //**********************************************************
    //** ctor
    //**********************************************************

    public Rasterizer(int width, int height)
    {
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
        if (_screen.Length < positions.Length)
            _screen = new Vector3[positions.Length];

        var worldViewProjection = world * viewProjection;
        for (var i = 0; i < positions.Length; i++)
        {
            var clip = Vector4.Transform(positions[i], worldViewProjection);
            if (clip.Z < 0) // in front of the near plane
            {
                _screen[i] = new Vector3(float.NaN);
                continue;
            }

            var inverseW = 1f / clip.W;
            _screen[i] = new Vector3(
                (clip.X * inverseW + 1f) * 0.5f * Width,
                (1f - clip.Y * inverseW) * 0.5f * Height,
                clip.Z * inverseW);
        }

        var indices = mesh.Indices;
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var a = _screen[indices[t * 3]];
            var b = _screen[indices[t * 3 + 1]];
            var c = _screen[indices[t * 3 + 2]];

            // shortcut: a triangle with a corner behind the near plane is dropped, not clipped. Fine while the
            // camera looks at the scene from outside; clip it once the camera drives inside one (a track).
            if (float.IsNaN(a.X) || float.IsNaN(b.X) || float.IsNaN(c.X))
                continue;

            // Counter-clockwise front faces turn clockwise (negative area) once screen y points down.
            var area = Edge(a, b, c.X, c.Y);
            if (area >= 0)
                continue;

            var normal = Vector3.Normalize(Vector3.TransformNormal(mesh.TriangleNormals[t], world));
            var light = Ambient + Diffuse * MathF.Max(0, Vector3.Dot(normal, toLight));
            Fill(a, b, c, area, mesh.TriangleColors[t] * light);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

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
                    if (depth < _depth[index])
                    {
                        _depth[index] = depth;
                        Colors[index] = ConsolePalette.ToConsoleColor(color, x, y);
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
