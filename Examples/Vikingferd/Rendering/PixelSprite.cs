using System;

namespace Vikingferd.Rendering;

/// <summary>
/// Pixel art for the <see cref="Canvas"/>, written as one string per pixel row with one character per pixel: a hex
/// digit is a <see cref="ConsoleColor"/> (<c>0</c> black … <c>f</c> white), <c>.</c> is transparent and <c>*</c> is
/// the accent colour passed to <see cref="Draw"/> (a switch's channel colour). Parsed once.
/// </summary>
public sealed class PixelSprite
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const sbyte Transparent = -1;
    private const sbyte Accent = -2;

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

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Throws <see cref="FormatException"/> for rows of different widths or an unknown character.</summary>
    public static PixelSprite Parse(params string[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var width = rows.Length == 0 ? 0 : rows[0].Length;
        var pixels = new sbyte[width * rows.Length];
        for (var y = 0; y < rows.Length; y++)
        {
            if (rows[y].Length != width)
                throw new FormatException($"Sprite row {y} is {rows[y].Length} pixels wide, expected {width}.");

            for (var x = 0; x < width; x++)
                pixels[y * width + x] = Code(rows[y][x]);
        }

        return new PixelSprite(width, rows.Length, pixels);
    }

    /// <summary>
    /// Draws with the top-left pixel at (<paramref name="x"/>, <paramref name="y"/>), each sprite pixel as a
    /// <paramref name="scale"/> x <paramref name="scale"/> block. <paramref name="mirror"/> flips it left-right;
    /// <paramref name="dim"/> draws every pixel dark grey (a knocked-out hero).
    /// </summary>
    public void Draw(Canvas canvas, int x, int y, ConsoleColor accent = ConsoleColor.White, bool mirror = false,
        bool dim = false, int scale = 1)
    {
        for (var row = 0; row < Height; row++)
        for (var column = 0; column < Width; column++)
        {
            var code = _pixels[row * Width + (mirror ? Width - 1 - column : column)];
            if (code == Transparent)
                continue;

            var color = dim ? ConsoleColor.DarkGray : code == Accent ? accent : (ConsoleColor)code;
            for (var dy = 0; dy < scale; dy++)
            for (var dx = 0; dx < scale; dx++)
                canvas.Pixel(x + column * scale + dx, y + row * scale + dy, color);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static sbyte Code(char symbol) => symbol switch
    {
        '.' => Transparent,
        '*' => Accent,
        >= '0' and <= '9' => (sbyte)(symbol - '0'),
        >= 'a' and <= 'f' => (sbyte)(symbol - 'a' + 10),
        _ => throw new FormatException($"Unknown sprite pixel '{symbol}'.")
    };
}
