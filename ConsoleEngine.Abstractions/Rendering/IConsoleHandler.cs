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
    void Render(Span<Pixel> pixels);
    void Close();
}