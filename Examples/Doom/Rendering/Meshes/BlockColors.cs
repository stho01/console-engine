using System;
using System.Numerics;
using Doom.Graphics;

namespace Doom.Rendering.Meshes;

/// <summary>
/// <c>ConsoleEngine.Rendering3D</c> colours whole triangles, it has no textures. So a wall texture or flat is cut
/// into <see cref="Size"/> × <see cref="Size"/> texel blocks, and each block becomes one quad in the block's average
/// colour: the texture as a mosaic. Averages are made once per texture or flat, on first use.
/// </summary>
public sealed class BlockColors
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Texels per block side, on walls and floors.</summary>
    public const int Size = 8;

    private readonly Palette _palette;
    private readonly TextureLibrary _textures;
    private readonly Blocks[] _textureBlocks;
    private readonly Blocks[] _flatBlocks;

    /// <summary>The blocks of one picture: colour (0..1 RGB) and the share of drawn texels, x-major.</summary>
    private sealed class Blocks(int across, int down)
    {
        public int Across { get; } = across;
        public int Down { get; } = down;
        public Vector3[] Colors { get; } = new Vector3[across * down];
        public float[] Coverage { get; } = new float[across * down];
    }

    //**********************************************************
    //** ctor
    //**********************************************************

    public BlockColors(Palette palette, TextureLibrary textures)
    {
        _palette = palette;
        _textures = textures;
        _textureBlocks = new Blocks[textures.Textures.Count];
        _flatBlocks = new Blocks[textures.Flats.Count];
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>A palette colour as 0..1 RGB (palette 0).</summary>
    public Vector3 ColorOf(byte index)
    {
        var (r, g, b) = _palette.GetColor(0, index);
        return new Vector3(r, g, b) / 255f;
    }

    /// <summary>
    /// The block of <paramref name="texture"/> holding texel column <paramref name="column"/> and row
    /// <paramref name="row"/> (both wrap): its colour and the share of its texels that are drawn.
    /// </summary>
    public (Vector3 Color, float Coverage) Texture(int texture, int column, int row)
    {
        var blocks = _textureBlocks[texture] ??= Average(_textures.Textures[texture]);
        var source = _textures.Textures[texture];
        var i = Wrap(column, source.Width) / Size * blocks.Down + Wrap(row, source.Height) / Size;
        return (blocks.Colors[i], blocks.Coverage[i]);
    }

    /// <summary>
    /// The colour of the block of <paramref name="flat"/> holding texel (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public Vector3 Flat(int flat, int x, int y)
    {
        var blocks = _flatBlocks[flat] ??= Average(_textures.Flats[flat]);
        return blocks.Colors[(x & Graphics.Flat.Mask) / Size * blocks.Down + (y & Graphics.Flat.Mask) / Size];
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static int Wrap(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private Blocks Average(Texture texture)
    {
        var blocks = new Blocks((texture.Width + Size - 1) / Size, (texture.Height + Size - 1) / Size);
        for (var bx = 0; bx < blocks.Across; bx++)
        for (var by = 0; by < blocks.Down; by++)
        {
            var sum = Vector3.Zero;
            int drawn = 0, total = 0;
            for (var x = bx * Size; x < Math.Min(texture.Width, (bx + 1) * Size); x++)
            for (var y = by * Size; y < Math.Min(texture.Height, (by + 1) * Size); y++)
            {
                total++;
                var i = x * texture.Height + y;
                if (!texture.Opaque[i])
                    continue;

                sum += ColorOf(texture.Pixels[i]);
                drawn++;
            }

            var block = bx * blocks.Down + by;
            blocks.Colors[block] = drawn > 0 ? sum / drawn : Vector3.Zero;
            blocks.Coverage[block] = total > 0 ? drawn / (float)total : 0;
        }

        return blocks;
    }

    private Blocks Average(Flat flat)
    {
        const int across = Graphics.Flat.Size / Size;
        var blocks = new Blocks(across, across);
        for (var bx = 0; bx < across; bx++)
        for (var by = 0; by < across; by++)
        {
            var sum = Vector3.Zero;
            for (var x = bx * Size; x < (bx + 1) * Size; x++)
            for (var y = by * Size; y < (by + 1) * Size; y++)
                sum += ColorOf(flat.Pixels[y * Graphics.Flat.Size + x]);

            blocks.Colors[bx * across + by] = sum / (Size * Size);
            blocks.Coverage[bx * across + by] = 1;
        }

        return blocks;
    }
}
