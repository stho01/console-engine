using System;
using ConsoleEngine.Rendering3D;

namespace Kartster.Ui;

/// <summary>
/// Draws into a palette-mode frame (<c>IRenderer3D.Colors</c>): each colour becomes the nearest of the palette's 16,
/// without dither, so text stays crisp. The nearest colour is worked out here from <see cref="ConsolePalette.Colors"/>
/// rather than through <c>ConsolePalette.ToConsoleColor</c>, which always dithers; that keeps
/// <c>ConsoleEngine.Rendering3D</c> untouched. UI pixels come in runs of one colour, so the last answer is kept.
/// </summary>
public sealed class PaletteTarget(ConsoleColor[] frame, int width, int height, ConsolePalette palette) : IPixelTarget
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly uint[] _colors = palette.Colors;
    private uint _lastRgb;
    private ConsoleColor _lastColor = Nearest(palette.Colors, 0);

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The frame drawn into, <c>index = y * Width + x</c>. Swap it when the renderer changes.</summary>
    public ConsoleColor[] Frame { get; set; } = frame;

    public int Width { get; } = width;
    public int Height { get; } = height;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Set(int x, int y, uint rgb)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        if (rgb != _lastRgb)
        {
            _lastRgb = rgb;
            _lastColor = Nearest(_colors, rgb);
        }

        Frame[y * Width + x] = _lastColor;
    }

    /// <summary>The palette colour closest to <paramref name="rgb"/> (squared RGB distance).</summary>
    public static ConsoleColor Nearest(uint[] colors, uint rgb)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < colors.Length; i++)
        {
            var r = (int)((colors[i] >> 16) & 0xFF) - (int)((rgb >> 16) & 0xFF);
            var g = (int)((colors[i] >> 8) & 0xFF) - (int)((rgb >> 8) & 0xFF);
            var b = (int)(colors[i] & 0xFF) - (int)(rgb & 0xFF);
            var distance = r * r + g * g + b * b;
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return (ConsoleColor)best;
    }
}
