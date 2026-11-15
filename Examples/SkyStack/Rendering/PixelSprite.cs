using System;

namespace SkyStack.Rendering;

/// <summary>
/// A small picture of palette indices, drawn with <see cref="PixelCanvas.Draw"/>. The colours come from the palette
/// given at draw time, so one sprite can be drawn normal, flashing or as a shadow. Build sprites once, as
/// <c>static readonly</c>.
/// </summary>
public sealed class PixelSprite
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The index of a pixel that isn't drawn.</summary>
    public const sbyte Clear = -1;

    /// <summary>The character for a clear pixel in <see cref="FromRows"/>.</summary>
    public const char ClearChar = '.';

    private readonly sbyte[] _pixels;

    //**********************************************************
    //** ctor
    //**********************************************************

    private PixelSprite(int width, int height, sbyte[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }

    /// <summary>The palette index at (<paramref name="x"/>, <paramref name="y"/>), or <see cref="Clear"/>.</summary>
    public sbyte this[int x, int y] => _pixels[y * Width + x];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// One string per row, all the same length. Each character is looked up in <paramref name="keys"/>: its position
    /// there is the palette index. <see cref="ClearChar"/> is a clear pixel.
    /// </summary>
    public static PixelSprite FromRows(string keys, params string[] rows)
    {
        if (rows.Length == 0)
            throw new ArgumentException("A sprite needs at least one row.", nameof(rows));

        var width = rows[0].Length;
        var pixels = new sbyte[width * rows.Length];
        for (var y = 0; y < rows.Length; y++)
        {
            if (rows[y].Length != width)
                throw new ArgumentException($"Row {y} is {rows[y].Length} wide, expected {width}.", nameof(rows));

            for (var x = 0; x < width; x++)
            {
                var key = rows[y][x];
                var index = key == ClearChar ? Clear : keys.IndexOf(key);
                if (key != ClearChar && index < 0)
                    throw new ArgumentException($"Unknown pixel '{key}' in row {y}.", nameof(rows));
                pixels[y * width + x] = (sbyte)index;
            }
        }

        return new PixelSprite(width, rows.Length, pixels);
    }
}
