using System;
using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// The 16 RGB colours the console shows, and how to turn any RGB colour (0..1) into one of them. A 4×4 ordered
/// dither nudges each pixel before picking the nearest colour, so smooth shading shows up as a fine pattern of the
/// two nearest colours instead of hard bands. The closer the palette's colours, the finer the pattern: give the
/// console a palette made for the scene (<c>RenderConsole.Palette</c> in ConsoleEngine).
/// </summary>
public sealed class ConsolePalette
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int LookupBits = 5;
    private const int LookupSize = 1 << LookupBits;
    private const float DitherStrength = 0.12f;

    private static readonly float[] DitherOffsets = BuildDitherOffsets();
    private readonly ConsoleColor[] _nearest;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="colors">16 colours, <c>0xRRGGBB</c>, in <see cref="ConsoleColor"/> order.</param>
    public ConsolePalette(uint[] colors)
    {
        if (colors.Length != 16)
            throw new ArgumentException("A palette needs exactly 16 colours.", nameof(colors));

        Colors = colors;
        _nearest = BuildNearest(colors);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The Windows console's default colours ("Campbell"), what a console shows without a palette.</summary>
    public static ConsolePalette Campbell { get; } = new([
        0x0C0C0C, 0x0037DA, 0x13A10E, 0x3A96DD, 0xC50F1F, 0x881798, 0xC19C00, 0xCCCCCC,
        0x767676, 0x3B78FF, 0x16C60C, 0x61D6D6, 0xE74856, 0xB4009E, 0xF9F1A5, 0xF2F2F2
    ]);

    public uint[] Colors { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public ConsoleColor ToConsoleColor(Vector3 rgb, int x, int y)
    {
        var dithered = rgb + new Vector3(DitherOffsets[((y & 3) << 2) | (x & 3)]);
        var cell = Vector3.Clamp(dithered, Vector3.Zero, Vector3.One) * (LookupSize - 1) + new Vector3(0.5f);
        return _nearest[((int)cell.X << (2 * LookupBits)) | ((int)cell.Y << LookupBits) | (int)cell.Z];
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static Vector3 ToVector(uint rgb) => new Vector3((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF) / 255f;

    private static float[] BuildDitherOffsets()
    {
        int[] bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        var offsets = new float[bayer.Length];
        for (var i = 0; i < bayer.Length; i++)
            offsets[i] = ((bayer[i] + 0.5f) / bayer.Length - 0.5f) * DitherStrength;
        return offsets;
    }

    private static ConsoleColor[] BuildNearest(uint[] colors)
    {
        var palette = new Vector3[colors.Length];
        for (var i = 0; i < colors.Length; i++)
            palette[i] = ToVector(colors[i]);

        var nearest = new ConsoleColor[LookupSize * LookupSize * LookupSize];
        for (var r = 0; r < LookupSize; r++)
        for (var g = 0; g < LookupSize; g++)
        for (var b = 0; b < LookupSize; b++)
        {
            var rgb = new Vector3(r, g, b) / (LookupSize - 1);
            var best = 0;
            for (var i = 1; i < palette.Length; i++)
            {
                if (Vector3.DistanceSquared(rgb, palette[i]) < Vector3.DistanceSquared(rgb, palette[best]))
                    best = i;
            }

            nearest[(r << (2 * LookupBits)) | (g << LookupBits) | b] = (ConsoleColor)best;
        }

        return nearest;
    }
}
