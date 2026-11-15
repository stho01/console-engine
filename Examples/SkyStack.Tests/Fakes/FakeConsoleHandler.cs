using System;
using ConsoleEngine.Abstractions.Rendering;

namespace SkyStack.Tests.Fakes;

/// <summary>A console that does nothing, so a <c>RenderConsole</c> frame buffer can be drawn into and read back.</summary>
public sealed class FakeConsoleHandler(int width, int height) : IConsoleHandler
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
