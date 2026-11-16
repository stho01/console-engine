using System.Collections.Generic;
using ConsoleEngine.Rendering3D;
using Doom.Graphics;

namespace Doom.Rendering.Meshes;

/// <summary>
/// Doom's palette-index pictures as <c>ConsoleEngine.Rendering3D</c> textures (<see cref="MeshTexture"/>, 24-bit
/// colours of palette 0, undrawn texels as holes): wall textures, flats and sprite pictures. Each is converted the
/// first time it is asked for and kept.
/// </summary>
public sealed class TextureCache(Palette palette, TextureLibrary textures)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const uint Opaque = 0xFF000000;

    private readonly MeshTexture[] _walls = new MeshTexture[textures.Textures.Count];
    private readonly MeshTexture[] _flats = new MeshTexture[textures.Flats.Count];
    private readonly Dictionary<Picture, MeshTexture> _pictures = [];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Wall texture <paramref name="texture"/> (an index into <see cref="TextureLibrary.Textures"/>).</summary>
    public MeshTexture Wall(int texture)
    {
        if (_walls[texture] != null)
            return _walls[texture];

        var source = textures.Textures[texture];
        return _walls[texture] = FromColumns(source.Width, source.Height, source.Pixels, source.Opaque);
    }

    /// <summary>Flat <paramref name="flat"/> (an index into <see cref="TextureLibrary.Flats"/>).</summary>
    public MeshTexture Flat(int flat)
    {
        if (_flats[flat] != null)
            return _flats[flat];

        var source = textures.Flats[flat].Pixels;
        var pixels = new uint[Graphics.Flat.Size * Graphics.Flat.Size];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = Opaque | Rgb(source[i]);
        return _flats[flat] = new MeshTexture(Graphics.Flat.Size, Graphics.Flat.Size, pixels);
    }

    /// <summary>A sprite picture.</summary>
    public MeshTexture Sprite(Picture picture)
    {
        if (!_pictures.TryGetValue(picture, out var texture))
        {
            texture = FromColumns(picture.Width, picture.Height, picture.Pixels, picture.Opaque);
            _pictures.Add(picture, texture);
        }

        return texture;
    }

    /// <summary>A palette colour as <c>0xRRGGBB</c> (palette 0).</summary>
    public uint Rgb(byte color)
    {
        var (r, g, b) = palette.GetColor(0, color);
        return (uint)(r << 16 | g << 8 | b);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>A picture stored column by column (<c>x * height + y</c>), as Doom's are, turned row by row.</summary>
    private MeshTexture FromColumns(int width, int height, byte[] pixels, bool[] opaque)
    {
        var texels = new uint[width * height];
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            var i = x * height + y;
            texels[y * width + x] = (opaque[i] ? Opaque : 0) | Rgb(pixels[i]);
        }

        return new MeshTexture(width, height, texels);
    }
}
