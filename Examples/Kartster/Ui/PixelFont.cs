using System;

namespace Kartster.Ui;

/// <summary>
/// A 3×5 bitmap font, one cell per pixel: the screen is 320×180 console cells of 3×3, so ordinary console text would
/// be unreadable. Monospaced (every glyph advances 4 pixels), so digits line up like tabular numbers. Lower case is
/// drawn as upper case; a character without a glyph leaves a gap. <c>scale</c> makes each font pixel a square of
/// that many pixels. Nothing here allocates.
/// </summary>
public static class PixelFont
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int GlyphWidth = 3;
    public const int GlyphHeight = 5;
    public const int Advance = GlyphWidth + 1;

    /// <summary>Every character that has a glyph.</summary>
    public const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,:/-+%!?()#><";

    private const ushort HasGlyphBit = 1 << 15;

    /// <summary>One string of 15 per character of <see cref="Characters"/>, in that order: 5 rows of 3, top to bottom,
    /// '#' = on. Digits are boxy (seven-segment like) so they read at a glance.</summary>
    public static readonly string[] GlyphRows =
    [
        ".#." + "#.#" + "###" + "#.#" + "#.#", // A
        "##." + "#.#" + "##." + "#.#" + "##.", // B
        ".##" + "#.." + "#.." + "#.." + ".##", // C
        "##." + "#.#" + "#.#" + "#.#" + "##.", // D
        "###" + "#.." + "##." + "#.." + "###", // E
        "###" + "#.." + "##." + "#.." + "#..", // F
        ".##" + "#.." + "#.#" + "#.#" + ".##", // G
        "#.#" + "#.#" + "###" + "#.#" + "#.#", // H
        "###" + ".#." + ".#." + ".#." + "###", // I
        "..#" + "..#" + "..#" + "#.#" + ".#.", // J
        "#.#" + "#.#" + "##." + "#.#" + "#.#", // K
        "#.." + "#.." + "#.." + "#.." + "###", // L
        "#.#" + "###" + "###" + "#.#" + "#.#", // M
        "##." + "#.#" + "#.#" + "#.#" + "#.#", // N
        ".#." + "#.#" + "#.#" + "#.#" + ".#.", // O
        "##." + "#.#" + "##." + "#.." + "#..", // P
        ".#." + "#.#" + "#.#" + "##." + ".##", // Q
        "##." + "#.#" + "##." + "#.#" + "#.#", // R
        ".##" + "#.." + ".#." + "..#" + "##.", // S
        "###" + ".#." + ".#." + ".#." + ".#.", // T
        "#.#" + "#.#" + "#.#" + "#.#" + "###", // U
        "#.#" + "#.#" + "#.#" + "#.#" + ".#.", // V
        "#.#" + "#.#" + "###" + "###" + "#.#", // W
        "#.#" + "#.#" + ".#." + "#.#" + "#.#", // X
        "#.#" + "#.#" + ".#." + ".#." + ".#.", // Y
        "###" + "..#" + ".#." + "#.." + "###", // Z
        "###" + "#.#" + "#.#" + "#.#" + "###", // 0
        ".#." + "##." + ".#." + ".#." + "###", // 1
        "###" + "..#" + "###" + "#.." + "###", // 2
        "###" + "..#" + "###" + "..#" + "###", // 3
        "#.#" + "#.#" + "###" + "..#" + "..#", // 4
        "###" + "#.." + "###" + "..#" + "###", // 5
        "###" + "#.." + "###" + "#.#" + "###", // 6
        "###" + "..#" + "..#" + "..#" + "..#", // 7
        "###" + "#.#" + "###" + "#.#" + "###", // 8
        "###" + "#.#" + "###" + "..#" + "###", // 9
        "..." + "..." + "..." + "..." + "...", // space
        "..." + "..." + "..." + "..." + ".#.", // .
        "..." + "..." + "..." + ".#." + "#..", // ,
        "..." + ".#." + "..." + ".#." + "...", // :
        "..#" + "..#" + ".#." + "#.." + "#..", // /
        "..." + "..." + "###" + "..." + "...", // -
        "..." + ".#." + "###" + ".#." + "...", // +
        "#.#" + "..#" + ".#." + "#.." + "#.#", // %
        ".#." + ".#." + ".#." + "..." + ".#.", // !
        "##." + "..#" + ".#." + "..." + ".#.", // ?
        "..#" + ".#." + ".#." + ".#." + "..#", // (
        "#.." + ".#." + ".#." + ".#." + "#..", // )
        "#.#" + "###" + "#.#" + "###" + "#.#", // #
        "#.." + ".#." + "..#" + ".#." + "#..", // >
        "..#" + ".#." + "#.." + ".#." + "..#", // <
    ];

    // After GlyphRows: static fields initialise in order.
    private static readonly ushort[] Glyphs = BuildGlyphs();

    //**********************************************************
    //** public methods
    //**********************************************************

    public static bool HasGlyph(char c) => (Glyph(c) & HasGlyphBit) != 0;

    /// <summary>Width in pixels of <paramref name="charCount"/> characters (no gap after the last).</summary>
    public static int MeasureChars(int charCount, int scale = 1) => charCount <= 0 ? 0 : (charCount * Advance - 1) * scale;

    public static int MeasureText(ReadOnlySpan<char> text, int scale = 1) => MeasureChars(text.Length, scale);

    /// <summary>Line height in pixels: the glyph plus one pixel of gap, scaled.</summary>
    public static int LineHeight(int scale = 1) => (GlyphHeight + 1) * scale;

    /// <summary>Draws <paramref name="text"/> with its top-left at (x, y).</summary>
    /// <returns>The x where the next character would go.</returns>
    public static int DrawText(IPixelTarget target, int x, int y, ReadOnlySpan<char> text, uint rgb, int scale = 1)
    {
        for (var i = 0; i < text.Length; i++)
        {
            DrawGlyph(target, x, y, Glyph(text[i]), rgb, scale);
            x += Advance * scale;
        }

        return x;
    }

    /// <summary>
    /// Draws <paramref name="text"/> with a one-pixel ring of <paramref name="outlineRgb"/> round every font pixel,
    /// so it reads over the 3D scene.
    /// </summary>
    /// <returns>The x where the next character would go.</returns>
    public static int DrawTextOutlined(
        IPixelTarget target, int x, int y, ReadOnlySpan<char> text, uint rgb, uint outlineRgb, int scale = 1)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (dx != 0 || dy != 0)
                DrawText(target, x + dx, y + dy, text, outlineRgb, scale);
        }

        return DrawText(target, x, y, text, rgb, scale);
    }

    /// <summary>Draws <paramref name="text"/> centred on <paramref name="centerX"/>.</summary>
    public static void DrawCentered(
        IPixelTarget target, int centerX, int y, ReadOnlySpan<char> text, uint rgb, uint outlineRgb, int scale = 1)
        => DrawTextOutlined(target, centerX - MeasureText(text, scale) / 2, y, text, rgb, outlineRgb, scale);

    //**********************************************************
    //** internals
    //**********************************************************

    private static ushort Glyph(char c)
    {
        if (c is >= 'a' and <= 'z')
            c = (char)(c - 32);
        return c < Glyphs.Length ? Glyphs[c] : (ushort)0;
    }

    private static void DrawGlyph(IPixelTarget target, int x, int y, ushort glyph, uint rgb, int scale)
    {
        for (var row = 0; row < GlyphHeight; row++)
        for (var column = 0; column < GlyphWidth; column++)
        {
            if ((glyph & (1 << (row * GlyphWidth + column))) == 0)
                continue;

            for (var sy = 0; sy < scale; sy++)
            for (var sx = 0; sx < scale; sx++)
                target.Set(x + column * scale + sx, y + row * scale + sy, rgb);
        }
    }

    private static ushort[] BuildGlyphs()
    {
        var glyphs = new ushort[128];
        for (var i = 0; i < Characters.Length; i++)
        {
            var rows = GlyphRows[i];
            ushort bits = HasGlyphBit;
            for (var bit = 0; bit < GlyphWidth * GlyphHeight; bit++)
            {
                if (rows[bit] == '#')
                    bits |= (ushort)(1 << bit);
            }

            glyphs[Characters[i]] = bits;
        }

        return glyphs;
    }
}
