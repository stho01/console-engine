using Doom.Graphics;
using ConsoleEngine.Infrastructure.Rendering;

namespace Doom.Rendering;

/// <summary>
/// Copies a <see cref="FrameBuffer"/> to the console: two pixels per cell as an upper half block, the upper pixel
/// in the foreground colour and the lower one in the background. Each palette colour is shown exactly on a 24-bit
/// console, else dithered to the console's 16 by <see cref="ConsoleShades"/>.
/// </summary>
public static class Presenter
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const char HalfBlock = '▀';

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Draws the frame: in exact colours (<see cref="ConsoleShades.Rgb"/>) while the console shows 24-bit colour
    /// (<see cref="RenderConsole.TrueColorActive"/>), else dithered to the 16 console colours.
    /// </summary>
    /// <param name="tint">The <c>PLAYPAL</c> palette to show the frame through (pain and pickup tints).</param>
    public static void Present(FrameBuffer frame, ConsoleShades shades, int tint, RenderConsole console,
        int firstRow = 0)
    {
        var palette = tint < shades.PaletteCount ? tint : 0;
        if (console.TrueColorActive)
            PresentRgb(frame, shades, palette, console, firstRow);
        else
            PresentDithered(frame, shades, palette, console, firstRow);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void PresentRgb(FrameBuffer frame, ConsoleShades shades, int palette, RenderConsole console,
        int firstRow)
    {
        var pixels = frame.Pixels;
        var width = frame.Width;
        for (var row = 0; row < frame.Height / 2; row++)
        {
            var upper = row * 2 * width;
            var lower = upper + width;
            for (var x = 0; x < width; x++)
            {
                console.DrawRgb(x, firstRow + row, HalfBlock, shades.Rgb(palette, pixels[upper + x]),
                    shades.Rgb(palette, pixels[lower + x]));
            }
        }
    }

    private static void PresentDithered(FrameBuffer frame, ConsoleShades shades, int palette, RenderConsole console,
        int firstRow)
    {
        var pixels = frame.Pixels;
        var width = frame.Width;
        for (var row = 0; row < frame.Height / 2; row++)
        {
            var upperY = row * 2;
            var lowerY = upperY + 1;
            for (var x = 0; x < width; x++)
            {
                var upper = shades.Pick(palette, pixels[upperY * width + x], x, upperY);
                var lower = shades.Pick(palette, pixels[lowerY * width + x], x, lowerY);
                console.Draw(x, firstRow + row, HalfBlock, upper, lower);
            }
        }
    }
}
