using System;
using System.Numerics;

namespace KartViewer.Rendering;

/// <summary>
/// Turns RGB colours (0..1) into the 16 console colours. A 4×4 ordered dither nudges each pixel before picking the
/// nearest colour, so smooth shading shows up as a pattern of the two nearest colours instead of hard bands.
/// </summary>
public static class ConsolePalette
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int LookupBits = 5;
    private const int LookupSize = 1 << LookupBits;
    private const float DitherStrength = 0.3f;

    // shortcut: assumes the "Campbell" scheme (conhost and Windows Terminal default); set the console palette
    // ourselves (SetConsoleScreenBufferInfoEx) once colours have to be exact.
    private static readonly Vector3[] Colors =
    [
        Rgb(12, 12, 12),    // Black
        Rgb(0, 55, 218),    // DarkBlue
        Rgb(19, 161, 14),   // DarkGreen
        Rgb(58, 150, 221),  // DarkCyan
        Rgb(197, 15, 31),   // DarkRed
        Rgb(136, 23, 152),  // DarkMagenta
        Rgb(193, 156, 0),   // DarkYellow
        Rgb(204, 204, 204), // Gray
        Rgb(118, 118, 118), // DarkGray
        Rgb(59, 120, 255),  // Blue
        Rgb(22, 198, 12),   // Green
        Rgb(97, 214, 214),  // Cyan
        Rgb(231, 72, 86),   // Red
        Rgb(180, 0, 158),   // Magenta
        Rgb(249, 241, 165), // Yellow
        Rgb(242, 242, 242)  // White
    ];

    private static readonly float[] DitherOffsets = BuildDitherOffsets();
    private static readonly ConsoleColor[] Nearest = BuildNearest();

    //**********************************************************
    //** public methods
    //**********************************************************

    public static ConsoleColor ToConsoleColor(Vector3 rgb, int x, int y)
    {
        var dithered = rgb + new Vector3(DitherOffsets[((y & 3) << 2) | (x & 3)]);
        var cell = Vector3.Clamp(dithered, Vector3.Zero, Vector3.One) * (LookupSize - 1) + new Vector3(0.5f);
        return Nearest[((int)cell.X << (2 * LookupBits)) | ((int)cell.Y << LookupBits) | (int)cell.Z];
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static Vector3 Rgb(int r, int g, int b) => new Vector3(r, g, b) / 255f;

    private static float[] BuildDitherOffsets()
    {
        int[] bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        var offsets = new float[bayer.Length];
        for (var i = 0; i < bayer.Length; i++)
            offsets[i] = ((bayer[i] + 0.5f) / bayer.Length - 0.5f) * DitherStrength;
        return offsets;
    }

    private static ConsoleColor[] BuildNearest()
    {
        var nearest = new ConsoleColor[LookupSize * LookupSize * LookupSize];
        for (var r = 0; r < LookupSize; r++)
        for (var g = 0; g < LookupSize; g++)
        for (var b = 0; b < LookupSize; b++)
        {
            var rgb = new Vector3(r, g, b) / (LookupSize - 1);
            var best = 0;
            for (var i = 1; i < Colors.Length; i++)
            {
                if (Vector3.DistanceSquared(rgb, Colors[i]) < Vector3.DistanceSquared(rgb, Colors[best]))
                    best = i;
            }

            nearest[(r << (2 * LookupBits)) | (g << LookupBits) | b] = (ConsoleColor)best;
        }

        return nearest;
    }
}
