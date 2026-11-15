using System;
using ConsoleEngine.Abstractions.Rendering;

namespace ConsoleEngine.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IConsoleHandler"/> that keeps a copy of the last rendered frame.
/// NSubstitute can't capture <see cref="Span{T}"/> arguments, so <see cref="IConsoleHandler.Render"/> needs a
/// hand-written fake.
/// </summary>
public class FakeConsoleHandler(int width, int height) : IConsoleHandler
{
    public int Width { get; } = width;
    public int Height { get; } = height;

    public Pixel[] LastFrame { get; private set; } = [];
    public FontInfo Font { get; private set; }
    public string Title { get; private set; }
    public bool? CursorVisible { get; private set; }
    public bool? IsResizable { get; private set; }
    public bool Initialized { get; private set; }
    public uint[] Palette { get; private set; }

    /// <summary>Whether <see cref="SetTrueColor"/> can turn 24-bit colour on.</summary>
    public bool SupportsTrueColor { get; set; } = true;
    public bool? TrueColorRequested { get; private set; }
    public uint[] LastForegroundRgb { get; private set; } = [];
    public uint[] LastBackgroundRgb { get; private set; } = [];

    public Pixel PixelAt(int x, int y) => LastFrame[y * Width + x];

    public void InitializeConsole() => Initialized = true;
    public void SetTitle(string title) => Title = title;
    public void SetCursorVisible(bool visible) => CursorVisible = visible;
    public void Resizable(bool resizable) => IsResizable = resizable;
    public void SetFont(FontInfo fontInfo) => Font = fontInfo;
    public void SetPalette(ReadOnlySpan<uint> colors) => Palette = colors.ToArray();
    public void Render(Span<Pixel> pixels) => LastFrame = pixels.ToArray();

    public bool SetTrueColor(bool trueColor)
    {
        TrueColorRequested = trueColor;
        return trueColor && SupportsTrueColor;
    }

    public void Render(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb)
    {
        LastFrame = pixels.ToArray();
        LastForegroundRgb = foregroundRgb.ToArray();
        LastBackgroundRgb = backgroundRgb.ToArray();
    }
    public void Close() { }
}
