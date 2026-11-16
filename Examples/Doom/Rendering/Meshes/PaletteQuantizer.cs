using Doom.Graphics;

namespace Doom.Rendering.Meshes;

/// <summary>
/// Turns the rasterizer's 24-bit colours back into Doom palette indices (the nearest colour of palette 0, looked up
/// per 6-bit-per-channel cell), so the rest of the game (weapon, tints, gamma, both console colour modes) works on
/// the mesh renderer's frame as on the BSP renderer's. Doom does the same with its light tables.
/// </summary>
public sealed class PaletteQuantizer
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int Bits = 6;
    private const int Cells = 1 << Bits;

    private readonly byte[] _nearest = new byte[Cells * Cells * Cells];

    //**********************************************************
    //** ctor
    //**********************************************************

    public PaletteQuantizer(Palette palette)
    {
        var red = new int[Palette.ColorCount];
        var green = new int[Palette.ColorCount];
        var blue = new int[Palette.ColorCount];
        for (var i = 0; i < Palette.ColorCount; i++)
            (red[i], green[i], blue[i]) = palette.GetColor(0, i);

        for (var r = 0; r < Cells; r++)
        for (var g = 0; g < Cells; g++)
        for (var b = 0; b < Cells; b++)
        {
            // The middle of the cell, in 0..255.
            int cr = (r << (8 - Bits)) + 2, cg = (g << (8 - Bits)) + 2, cb = (b << (8 - Bits)) + 2;
            var best = 0;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < Palette.ColorCount; i++)
            {
                int dr = red[i] - cr, dg = green[i] - cg, db = blue[i] - cb;
                var distance = dr * dr + dg * dg + db * db;
                if (distance >= bestDistance)
                    continue;

                best = i;
                bestDistance = distance;
            }

            _nearest[(r << (2 * Bits)) | (g << Bits) | b] = (byte)best;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The palette index nearest to <paramref name="rgb"/> (<c>0xRRGGBB</c>).</summary>
    public byte Nearest(uint rgb) =>
        _nearest[(int)(((rgb >> (24 - Bits)) & (Cells - 1)) << (2 * Bits)
                       | ((rgb >> (16 - Bits)) & (Cells - 1)) << Bits
                       | ((rgb >> (8 - Bits)) & (Cells - 1)))];
}
