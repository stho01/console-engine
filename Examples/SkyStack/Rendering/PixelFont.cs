using System;

namespace SkyStack.Rendering;

/// <summary>
/// A 3 × 5 pixel font for numbers and the letters of the logo, drawn into a <see cref="PixelCanvas"/> at any whole
/// scale. Unknown characters are drawn as a gap.
/// </summary>
public static class PixelFont
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int GlyphWidth = 3;
    public const int GlyphHeight = 5;

    /// <summary>Blank columns between two glyphs, before scaling.</summary>
    public const int Spacing = 1;

    private const int MaxDigits = 11;

    private static readonly string[][] Glyphs = BuildGlyphs();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Width in pixels of <paramref name="length"/> glyphs at <paramref name="scale"/>.</summary>
    public static int MeasureWidth(int length, int scale = 1) =>
        length <= 0 ? 0 : (length * (GlyphWidth + Spacing) - Spacing) * scale;

    public static void Draw(PixelCanvas canvas, int x, int y, ReadOnlySpan<char> text, ConsoleColor color,
        int scale = 1)
    {
        for (var i = 0; i < text.Length; i++)
            DrawGlyph(canvas, x + i * (GlyphWidth + Spacing) * scale, y, text[i], color, scale);
    }

    public static void DrawNumber(PixelCanvas canvas, int x, int y, int value, ConsoleColor color, int scale = 1)
    {
        Span<char> digits = stackalloc char[MaxDigits];
        value.TryFormat(digits, out var length);
        Draw(canvas, x, y, digits[..length], color, scale);
    }

    public static int MeasureNumber(int value, int scale = 1)
    {
        Span<char> digits = stackalloc char[MaxDigits];
        value.TryFormat(digits, out var length);
        return MeasureWidth(length, scale);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void DrawGlyph(PixelCanvas canvas, int x, int y, char character, ConsoleColor color, int scale)
    {
        var glyph = character < Glyphs.Length ? Glyphs[character] : null;
        if (glyph == null)
            return;

        for (var row = 0; row < GlyphHeight; row++)
        for (var column = 0; column < GlyphWidth; column++)
        {
            if (glyph[row][column] == '#')
                canvas.FillRect(x + column * scale, y + row * scale, scale, scale, color);
        }
    }

    private static string[][] BuildGlyphs()
    {
        var glyphs = new string[128][];
        glyphs['0'] = ["###", "#.#", "#.#", "#.#", "###"];
        glyphs['1'] = [".#.", "##.", ".#.", ".#.", "###"];
        glyphs['2'] = ["###", "..#", "###", "#..", "###"];
        glyphs['3'] = ["###", "..#", ".##", "..#", "###"];
        glyphs['4'] = ["#.#", "#.#", "###", "..#", "..#"];
        glyphs['5'] = ["###", "#..", "###", "..#", "###"];
        glyphs['6'] = ["###", "#..", "###", "#.#", "###"];
        glyphs['7'] = ["###", "..#", ".#.", ".#.", ".#."];
        glyphs['8'] = ["###", "#.#", "###", "#.#", "###"];
        glyphs['9'] = ["###", "#.#", "###", "..#", "###"];
        glyphs['A'] = [".#.", "#.#", "###", "#.#", "#.#"];
        glyphs['C'] = [".##", "#..", "#..", "#..", ".##"];
        glyphs['K'] = ["#.#", "#.#", "##.", "#.#", "#.#"];
        glyphs['S'] = [".##", "#..", ".#.", "..#", "##."];
        glyphs['T'] = ["###", ".#.", ".#.", ".#.", ".#."];
        glyphs['Y'] = ["#.#", "#.#", ".#.", ".#.", ".#."];
        return glyphs;
    }
}
