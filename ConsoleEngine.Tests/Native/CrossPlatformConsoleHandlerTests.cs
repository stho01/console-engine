using System;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.CrossPlatform;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Native;

/// <summary>The frame text <see cref="CrossPlatformConsoleHandler"/> builds, without a terminal.</summary>
public class CrossPlatformConsoleHandlerTests
{
    private const string Esc = "\u001b";
    private const uint Palette = IConsoleHandler.PaletteColor;

    [Test]
    public void SetTrueColor_TrueColorTerminal_TurnsOn() =>
        Assert.That(new CrossPlatformConsoleHandler(2, 1, trueColorTerminal: true).SetTrueColor(true), Is.True);

    [Test]
    public void SetTrueColor_SixteenColorTerminal_StaysOff() =>
        Assert.That(new CrossPlatformConsoleHandler(2, 1, trueColorTerminal: false).SetTrueColor(true), Is.False);

    [Test]
    public void BuildFrame_Rgb_WritesEachCellsRgbAndPaletteColorFromThePalette()
    {
        var handler = new CrossPlatformConsoleHandler(2, 1, trueColorTerminal: true);
        Pixel[] pixels = [Cell('A', ConsoleColor.White), Cell('B', ConsoleColor.White)];

        Assert.That(handler.BuildFrame(pixels, [0x102030, Palette], [0x405060, Palette], true, 2, 1), Is.True);

        var frame = handler.Frame.ToString();
        Assert.That(frame, Does.Contain($"{Esc}[38;2;16;32;48;48;2;64;80;96mA"));
        Assert.That(frame, Does.Contain($"{Esc}[38;2;242;242;242;48;2;0;0;0mB"), "Campbell white on black");
    }

    [Test]
    public void BuildFrame_RgbUnchangedThenOneCellChanged_WritesNothingThenOnlyThatCell()
    {
        var handler = new CrossPlatformConsoleHandler(2, 1, trueColorTerminal: true);
        Pixel[] pixels = [Cell('A', ConsoleColor.White), Cell('B', ConsoleColor.White)];
        uint[] foreground = [0x102030, 0x102030];
        uint[] background = [0, 0];
        handler.BuildFrame(pixels, foreground, background, true, 2, 1);

        Assert.That(handler.BuildFrame(pixels, foreground, background, true, 2, 1), Is.False);

        foreground[1] = 0xFF0000;
        Assert.That(handler.BuildFrame(pixels, foreground, background, true, 2, 1), Is.True);
        var frame = handler.Frame.ToString();
        Assert.That(frame, Does.Contain($"{Esc}[1;2H{Esc}[38;2;255;0;0;48;2;0;0;0mB"));
        Assert.That(frame, Does.Not.Contain("A"));
    }

    [Test]
    public void BuildFrame_PaletteFrameAfterRgb_RedrawsInAnsiColors()
    {
        var handler = new CrossPlatformConsoleHandler(1, 1, trueColorTerminal: false);
        Pixel[] pixels = [Cell('A', ConsoleColor.Red)];
        handler.BuildFrame(pixels, [0x102030], [0], true, 1, 1);

        Assert.That(handler.BuildFrame(pixels, default, default, false, 1, 1), Is.True);
        Assert.That(handler.Frame.ToString(), Does.Contain($"{Esc}[2J").And.Contain($"{Esc}[91;40mA"));
    }

    [Test]
    public void BuildFrame_RgbHalfBlocks_TopPixelIsForegroundBottomIsBackground()
    {
        var handler = new CrossPlatformConsoleHandler(1, 2, trueColorTerminal: true);
        handler.SetPixelMode(true);
        Pixel[] pixels = [Cell(' ', ConsoleColor.Black), Cell(' ', ConsoleColor.Black)];

        handler.BuildFrame(pixels, [Palette, Palette], [0xFF0000, 0x0000FF], true, 1, 1);

        Assert.That(handler.Frame.ToString(), Does.Contain($"{Esc}[38;2;255;0;0;48;2;0;0;255m▀"));
    }

    [Test]
    public void BuildFrame_RgbSteadyState_DoesNotAllocate()
    {
        var handler = new CrossPlatformConsoleHandler(40, 20, trueColorTerminal: true);
        var pixels = new Pixel[40 * 20];
        var foreground = new uint[pixels.Length];
        var background = new uint[pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = Cell('#', ConsoleColor.Gray);

        // Every frame changes every cell (so the whole frame is written each time) to colours as long as the last.
        for (var frame = 0; frame < 3; frame++)
            Fill(handler, pixels, foreground, background, (uint)frame);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 3; frame < 13; frame++)
            Fill(handler, pixels, foreground, background, (uint)frame);

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    private static void Fill(CrossPlatformConsoleHandler handler, Pixel[] pixels, uint[] foreground,
        uint[] background, uint frame)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            foreground[i] = 0x646464u + (frame & 1) * 0x010101u + (uint)(i & 7);
            background[i] = 0x101010u + (frame & 1);
        }

        handler.BuildFrame(pixels, foreground, background, true, 40, 20);
    }

    private static Pixel Cell(char c, ConsoleColor foreground) =>
        new() { Char = c, ForegroundColor = foreground, BackgroundColor = ConsoleColor.Black };
}
