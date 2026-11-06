using System;
using ConsoleEngine.Abstractions.Rendering;

namespace ConsoleEngine.Benchmarks;

/// <summary>Console handler that renders nowhere, so <see cref="Infrastructure.Rendering.RenderConsole"/> runs
/// without Win32.</summary>
public class NullConsoleHandler(int width, int height) : IConsoleHandler
{
    public int Width { get; } = width;
    public int Height { get; } = height;

    public void InitializeConsole() { }
    public void SetTitle(string title) { }
    public void SetCursorVisible(bool visible) { }
    public void Resizable(bool resizable) { }
    public void SetFont(FontInfo fontInfo) { }
    public void Render(Span<Pixel> pixels) { }
    public void Close() { }
}
