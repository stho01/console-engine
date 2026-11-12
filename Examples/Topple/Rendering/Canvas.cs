using System;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace Topple.Rendering;

/// <summary>
/// Draws into the <see cref="RenderConsole"/> and remembers the background colour of every cell, so glass, smoke and
/// sparks can be drawn over the sky or a block without painting a black box behind them (<see cref="DrawOver"/>). The
/// backdrop covers every cell each frame, so the backgrounds never go stale. World layers are moved by
/// <see cref="Shake"/>; the HUD (<see cref="Draw"/>, <see cref="Text"/>) is not.
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

    /// <summary>Offset of the world layers this frame, in cells (screen shake).</summary>
    public Point Shake { get; set; }

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

    /// <summary>As <see cref="Draw"/>, moved by <see cref="Shake"/>.</summary>
    public void DrawWorld(int x, int y, char character, ConsoleColor foreground, ConsoleColor background) =>
        Draw(x + Shake.X, y + Shake.Y, character, foreground, background);

    /// <summary>As <see cref="DrawOver"/>, moved by <see cref="Shake"/>.</summary>
    public void DrawWorldOver(int x, int y, char character, ConsoleColor foreground) =>
        DrawOver(x + Shake.X, y + Shake.Y, character, foreground);

    /// <summary>The background drawn last at a cell, black outside the screen.</summary>
    public ConsoleColor BackgroundAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _backgrounds[y * Width + x] : ConsoleColor.Black;

    /// <summary>Text for the HUD; returns the column after it.</summary>
    public int Text(int column, int row, string text, ConsoleColor foreground, ConsoleColor background)
    {
        for (var i = 0; i < text.Length; i++)
            Draw(column + i, row, text[i], foreground, background);
        return column + text.Length;
    }

    /// <summary>A non-negative number, digit by digit, so no string is allocated; returns the column after it.</summary>
    public int Number(int column, int row, int value, ConsoleColor foreground, ConsoleColor background)
    {
        value = Math.Max(0, value);
        var digits = DigitCount(value);
        for (var d = digits - 1; d >= 0; d--, value /= 10)
            Draw(column + d, row, (char)('0' + value % 10), foreground, background);
        return column + digits;
    }

    public static int DigitCount(int value)
    {
        var digits = 1;
        for (var rest = value / 10; rest > 0; rest /= 10)
            digits++;
        return digits;
    }
}
