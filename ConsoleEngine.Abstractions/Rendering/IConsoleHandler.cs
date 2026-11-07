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
    void Render(Span<Pixel> pixels);
    void Close();
}