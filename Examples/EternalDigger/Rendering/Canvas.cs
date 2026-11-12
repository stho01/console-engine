using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace EternalDigger.Rendering;

/// <summary>
/// Draws into the <see cref="RenderConsole"/> and remembers the background colour of every screen cell, so effects
/// can be drawn over the sky, the house or a tile without painting a black box behind them
/// (<see cref="DrawOver"/>). The sky and terrain cover every cell each frame, so the backgrounds never go stale.
/// </summary>
public sealed class Canvas(RenderConsole console)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly ConsoleColor[] _backgrounds = new ConsoleColor[console.Width * console.Height];

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; } = console.Width;
    public int Height { get; } = console.Height;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Draw(int x, int y, char character, ConsoleColor foreground, ConsoleColor background)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        _backgrounds[y * Width + x] = background;
        console.Draw(x, y, character, foreground, background);
    }

    /// <summary>Draws a character and keeps the background that is already there.</summary>
    public void DrawOver(int x, int y, char character, ConsoleColor foreground) =>
        Draw(x, y, character, foreground, BackgroundAt(x, y));

    public void Draw(int x, int y, ReadOnlySpan<char> text, ConsoleColor foreground, ConsoleColor background)
    {
        for (var i = 0; i < text.Length; i++)
            Draw(x + i, y, text[i], foreground, background);
    }

    /// <summary>The background drawn last at a cell, black outside the screen.</summary>
    public ConsoleColor BackgroundAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _backgrounds[y * Width + x] : ConsoleColor.Black;
}
