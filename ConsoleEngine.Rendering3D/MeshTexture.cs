using System;
using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// A picture for textured triangles (<see cref="Mesh.TriangleTextures"/>): <c>0xAARRGGBB</c> per texel, row by row
/// (<c>y * Width + x</c>, y down from the top). A texel with alpha below 128 is a hole: textured pixels there are
/// not drawn and write no depth (fences, grates, sprites). Coordinates wrap, so a texture repeats. Sampled nearest
/// texel, no filtering. The texels are read once, at construction.
/// </summary>
public sealed class MeshTexture
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const uint AlphaThreshold = 0x80000000;

    private readonly Vector3[] _colors;
    private readonly bool[] _opaque;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="pixels"><c>0xAARRGGBB</c>, <paramref name="width"/> × <paramref name="height"/>, row by row.</param>
    public MeshTexture(int width, int height, ReadOnlySpan<uint> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length < width * height)
            throw new ArgumentException("A texture needs width × height pixels.", nameof(pixels));

        Width = width;
        Height = height;
        _colors = new Vector3[width * height];
        _opaque = new bool[width * height];
        for (var i = 0; i < _colors.Length; i++)
        {
            var pixel = pixels[i];
            _colors[i] = new Vector3((pixel >> 16) & 0xFF, (pixel >> 8) & 0xFF, pixel & 0xFF) / 255f;
            _opaque[i] = pixel >= AlphaThreshold;
            HasHoles |= !_opaque[i];
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }

    /// <summary>Whether any texel is a hole.</summary>
    public bool HasHoles { get; }

    /// <summary>The colours (0..1 RGB), <c>y * Width + x</c>.</summary>
    internal ReadOnlySpan<Vector3> Colors => _colors;

    internal ReadOnlySpan<bool> Opaque => _opaque;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// The texel at texture coordinates (<paramref name="u"/>, <paramref name="v"/>): 0..1 across the texture, both
    /// wrapping; false when it is a hole.
    /// </summary>
    public bool Sample(float u, float v, out Vector3 color)
    {
        var index = IndexOf(u, v);
        color = _colors[index];
        return _opaque[index];
    }

    //**********************************************************
    //** internals
    //**********************************************************

    internal int IndexOf(float u, float v)
    {
        var x = (int)MathF.Floor(u * Width) % Width;
        var y = (int)MathF.Floor(v * Height) % Height;
        if (x < 0)
            x += Width;
        if (y < 0)
            y += Height;
        return y * Width + x;
    }
}
