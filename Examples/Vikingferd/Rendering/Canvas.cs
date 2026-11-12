using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace Vikingferd.Rendering;

/// <summary>
/// A frame of square pixels, two per console cell (top and bottom half), plus text glyphs on top. Everything is drawn
/// here first; <see cref="Flush"/> turns each cell into one console character: a glyph over its pixels' colour, or
/// <c>▀</c> with the top pixel as foreground and the bottom one as background. With the 8 x 16 font the pixels are
/// square, so a 4 x 2 cell tile is 4 x 4 pixels.
/// </summary>
public sealed class Canvas
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const char UpperHalf = '▀';
    public const char Full = '█';

    private const byte NoBackground = 255;

    private readonly ConsoleColor[] _pixels;
    private readonly char[] _glyphs;
    private readonly ConsoleColor[] _glyphColors;
    private readonly byte[] _glyphBackgrounds;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Canvas(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new ConsoleColor[width * height * 2];
        _glyphs = new char[width * height];
        _glyphColors = new ConsoleColor[width * height];
        _glyphBackgrounds = new byte[width * height];
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Cells.</summary>
    public int Width { get; }

    public int Height { get; }
    public int PixelHeight => Height * 2;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Clear(ConsoleColor color)
    {
        Array.Fill(_pixels, color);
        Array.Clear(_glyphs);
    }

    /// <summary>Replaces every pixel (row by row, <see cref="Width"/> x <see cref="PixelHeight"/>) and every glyph.</summary>
    public void Fill(ReadOnlySpan<ConsoleColor> pixels)
    {
        pixels.CopyTo(_pixels);
        Array.Clear(_glyphs);
    }

    /// <summary>Text over the pixels (the bottom pixel shows behind each character). Spaces are skipped.</summary>
    public void Text(int x, int y, ReadOnlySpan<char> text, ConsoleColor color)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != ' ')
                Glyph(x + i, y, text[i], color);
        }
    }

    /// <summary>Sets a pixel (outside the canvas: ignored) and removes any glyph from its cell.</summary>
    public void Pixel(int x, int y, ConsoleColor color)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)PixelHeight)
            return;

        _pixels[y * Width + x] = color;
        _glyphs[(y >> 1) * Width + x] = '\0';
    }

    public ConsoleColor PixelAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)PixelHeight ? _pixels[y * Width + x] : ConsoleColor.Black;

    /// <summary>A character over whatever pixels the cell has (the bottom one shows behind it).</summary>
    public void Glyph(int x, int y, char character, ConsoleColor color)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        var i = y * Width + x;
        _glyphs[i] = character;
        _glyphColors[i] = color;
        _glyphBackgrounds[i] = NoBackground;
    }

    /// <summary>Text with its own background, over everything.</summary>
    public void Text(int x, int y, ReadOnlySpan<char> text, ConsoleColor color, ConsoleColor background)
    {
        if ((uint)y >= (uint)Height)
            return;

        for (var i = 0; i < text.Length; i++)
        {
            if ((uint)(x + i) >= (uint)Width)
                continue;

            var cell = y * Width + x + i;
            _glyphs[cell] = text[i];
            _glyphColors[cell] = color;
            _glyphBackgrounds[cell] = (byte)background;
        }
    }

    /// <summary>What a cell will look like on the console.</summary>
    public void Cell(int x, int y, out char character, out ConsoleColor foreground, out ConsoleColor background)
    {
        var i = y * Width + x;
        var top = _pixels[y * 2 * Width + x];
        var bottom = _pixels[(y * 2 + 1) * Width + x];
        if (_glyphs[i] != '\0')
        {
            character = _glyphs[i];
            foreground = _glyphColors[i];
            background = _glyphBackgrounds[i] == NoBackground ? bottom : (ConsoleColor)_glyphBackgrounds[i];
            return;
        }

        character = top == bottom ? ' ' : UpperHalf;
        foreground = top;
        background = bottom;
    }

    /// <summary>Writes every cell to the console's frame buffer.</summary>
    public void Flush(RenderConsole console)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            Cell(x, y, out var character, out var foreground, out var background);
            console.Draw(x, y, character, foreground, background);
        }
    }
}
