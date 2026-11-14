using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace SkyStack.Rendering;

/// <summary>
/// A grid of coloured square pixels shown two per console cell: <see cref="Present"/> draws every cell as
/// <see cref="HalfBlock"/> with the upper pixel as foreground and the lower one as background. With a font twice as
/// tall as it is wide, the pixels are square. Draw into it every frame, then present it once.
/// </summary>
public sealed class PixelCanvas
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Upper half block: the foreground colours the top half of the cell, the background the bottom half.</summary>
    public const char HalfBlock = '▀';

    private readonly ConsoleColor[] _pixels;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="width">Pixels, one per console column.</param>
    /// <param name="height">Pixels, two per console row; must be even.</param>
    public PixelCanvas(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (height % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Must be even: two pixels per cell.");

        Width = width;
        Height = height;
        _pixels = new ConsoleColor[width * height];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Set(int x, int y, ConsoleColor color)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            _pixels[y * Width + x] = color;
    }

    /// <summary>The pixel's colour, black outside the canvas.</summary>
    public ConsoleColor Get(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _pixels[y * Width + x] : ConsoleColor.Black;

    public void Fill(ConsoleColor color) => Array.Fill(_pixels, color);

    public void FillRect(int x, int y, int width, int height, ConsoleColor color)
    {
        var left = Math.Max(0, x);
        var right = Math.Min(Width, x + width);
        var top = Math.Max(0, y);
        var bottom = Math.Min(Height, y + height);
        for (var row = top; row < bottom; row++)
            _pixels.AsSpan(row * Width + left, Math.Max(0, right - left)).Fill(color);
    }

    /// <summary>
    /// Draws <paramref name="sprite"/> with its top-left pixel at (<paramref name="x"/>, <paramref name="y"/>), each
    /// of its palette indices looked up in <paramref name="palette"/>. Clear pixels are skipped.
    /// </summary>
    public void Draw(PixelSprite sprite, int x, int y, ReadOnlySpan<ConsoleColor> palette, bool mirrored = false)
    {
        for (var row = 0; row < sprite.Height; row++)
        for (var column = 0; column < sprite.Width; column++)
        {
            var index = sprite[mirrored ? sprite.Width - 1 - column : column, row];
            if (index != PixelSprite.Clear)
                Set(x + column, y + row, palette[index]);
        }
    }

    /// <summary>
    /// Writes the canvas into the console, with its top-left pixel in cell (<paramref name="left"/>,
    /// <paramref name="top"/>).
    /// </summary>
    public void Present(RenderConsole console, int left = 0, int top = 0)
    {
        for (var row = 0; row < Height / 2; row++)
        {
            var upper = row * 2 * Width;
            var lower = upper + Width;
            for (var column = 0; column < Width; column++)
                console.Draw(left + column, top + row, HalfBlock, _pixels[upper + column], _pixels[lower + column]);
        }
    }
}
