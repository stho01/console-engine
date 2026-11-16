using System;

namespace Doom.Rendering;

/// <summary>
/// The 3D view as palette indices (<c>y * Width + x</c>) with a depth per pixel (distance along the view direction,
/// <see cref="float.MaxValue"/> where nothing was drawn). Sprites and see-through walls are depth tested against it.
/// </summary>
public sealed class FrameBuffer
{
    //**********************************************************
    //** ctor
    //**********************************************************

    public FrameBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Pixels = new byte[width * height];
        Depth = new float[width * height];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public float[] Depth { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Clear(byte color)
    {
        Array.Fill(Pixels, color);
        Array.Fill(Depth, float.MaxValue);
    }

    public byte Get(int x, int y) => Pixels[y * Width + x];

    public void Set(int x, int y, byte color)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            Pixels[y * Width + x] = color;
    }

    /// <summary>Draws a line in screen pixels (automap).</summary>
    public void DrawLine(int x0, int y0, int x1, int y1, byte color)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        var guard = 0;
        while (guard++ < 4 * (Width + Height))
        {
            Set(x0, y0, color);
            if (x0 == x1 && y0 == y1)
                return;

            var doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }
}
