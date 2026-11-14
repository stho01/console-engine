using System;

namespace ConsoleEngine.Abstractions.Rendering;

public interface IConsoleHandler
{
    public int Width { get; }
    public int Height { get; }
        
    void InitializeConsole();
    void SetTitle(string title);
    void SetCursorVisible(bool visible);
    void Resizable(bool resizable);
    void SetFont(FontInfo fontInfo);

    /// <summary>
    /// Show every cell as a solid colored pixel, ignoring its character, for consoles whose character cells
    /// aren't square. Called before <see cref="InitializeConsole"/>.
    /// </summary>
    void SetPixelMode(bool pixelMode) { }

    /// <summary>
    /// Replaces the RGB colour each <see cref="ConsoleColor"/> shows as: 16 entries, <c>0xRRGGBB</c>, in
    /// <see cref="ConsoleColor"/> order. Called after <see cref="InitializeConsole"/>. Consoles that can't change their
    /// colours ignore it.
    /// </summary>
    void SetPalette(ReadOnlySpan<uint> colors) { }

    /// <summary>
    /// A value in the RGB spans of <see cref="Render(Span{Pixel}, ReadOnlySpan{uint}, ReadOnlySpan{uint})"/> that means
    /// "this cell shows its <see cref="Pixel"/>'s <see cref="ConsoleColor"/>" (as the palette shows it).
    /// </summary>
    const uint PaletteColor = 0xFFFFFFFF;

    /// <summary>
    /// Turns 24-bit colour output on or off; after <see cref="InitializeConsole"/>, also while the game runs. Returns
    /// whether the console now shows the RGB colours of
    /// <see cref="Render(Span{Pixel}, ReadOnlySpan{uint}, ReadOnlySpan{uint})"/>. Consoles that can't return false.
    /// </summary>
    bool SetTrueColor(bool trueColor) => false;

    void Render(Span<Pixel> pixels);

    /// <summary>
    /// Renders a 24-bit frame, called instead of <see cref="Render(Span{Pixel})"/> while <see cref="SetTrueColor"/>
    /// returned true. <paramref name="foregroundRgb"/> and <paramref name="backgroundRgb"/> hold <c>0xRRGGBB</c> per
    /// cell, or <see cref="PaletteColor"/>; <paramref name="pixels"/> holds the characters. Consoles without 24-bit
    /// colour show the pixels' own colours.
    /// </summary>
    void Render(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb) =>
        Render(pixels);

    void Close();
}