using System;

namespace Kartster.Ui;

/// <summary>Filled rectangles, frames, lines and gradients on an <see cref="IPixelTarget"/>, clipped to it.</summary>
public static class Shapes
{
    public static void FillRect(IPixelTarget target, int x, int y, int width, int height, uint rgb)
    {
        var right = Math.Min(x + width, target.Width);
        var bottom = Math.Min(y + height, target.Height);
        for (var py = Math.Max(y, 0); py < bottom; py++)
        for (var px = Math.Max(x, 0); px < right; px++)
            target.Set(px, py, rgb);
    }

    /// <summary>A one-pixel outline just inside the rectangle.</summary>
    public static void Frame(IPixelTarget target, int x, int y, int width, int height, uint rgb)
    {
        FillRect(target, x, y, width, 1, rgb);
        FillRect(target, x, y + height - 1, width, 1, rgb);
        FillRect(target, x, y + 1, 1, height - 2, rgb);
        FillRect(target, x + width - 1, y + 1, 1, height - 2, rgb);
    }

    /// <summary>Top row <paramref name="top"/>, bottom row <paramref name="bottom"/>, blended per row.</summary>
    public static void VerticalGradient(IPixelTarget target, int x, int y, int width, int height, uint top, uint bottom)
    {
        for (var row = 0; row < height; row++)
            FillRect(target, x, y + row, width, 1, Lerp(top, bottom, height > 1 ? row / (float)(height - 1) : 0));
    }

    /// <summary>Bresenham line, both ends included.</summary>
    public static void Line(IPixelTarget target, int x0, int y0, int x1, int y1, uint rgb)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var stepX = x0 < x1 ? 1 : -1;
        var stepY = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            target.Set(x0, y0, rgb);
            if (x0 == x1 && y0 == y1)
                return;

            var doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y0 += stepY;
            }
        }
    }

    public static uint Lerp(uint from, uint to, float t)
    {
        var r = Channel(from >> 16, to >> 16, t);
        var g = Channel(from >> 8, to >> 8, t);
        var b = Channel(from, to, t);
        return (r << 16) | (g << 8) | b;
    }

    private static uint Channel(uint from, uint to, float t)
    {
        var a = (int)(from & 0xFF);
        return (uint)(a + ((int)(to & 0xFF) - a) * t + 0.5f);
    }
}
