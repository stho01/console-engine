using System;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Tests.Fakes;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Infrastructure.Rendering;

public class RenderConsoleTrueColorTests
{
    private const int Width = 4;
    private const int Height = 3;

    private FakeConsoleHandler _handler;
    private RenderConsole _console;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeConsoleHandler(Width, Height);
        _console = new RenderConsole(_handler);
    }

    [Test]
    public void Initialize_TrueColorOff_NeverAsksTheConsole()
    {
        _console.Initialize();
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.TrueColorRequested, Is.Null);
            Assert.That(_console.TrueColorActive, Is.False);
            Assert.That(_handler.LastForegroundRgb, Is.Empty);
        });
    }

    [Test]
    public void DrawRgb_TrueColorActive_RendersTheRgbColours()
    {
        _console.TrueColor = true;
        _console.Initialize();

        _console.DrawRgb(1, 2, '#', 0x102030, 0x405060);
        _console.Display();

        var index = 2 * Width + 1;
        Assert.Multiple(() =>
        {
            Assert.That(_console.TrueColorActive, Is.True);
            Assert.That(_handler.PixelAt(1, 2).Char, Is.EqualTo('#'));
            Assert.That(_handler.LastForegroundRgb[index], Is.EqualTo(0x102030));
            Assert.That(_handler.LastBackgroundRgb[index], Is.EqualTo(0x405060));
        });
    }

    [Test]
    public void Initialize_TrueColorActive_StartsEveryCellOnThePalette()
    {
        _console.TrueColor = true;
        _console.Initialize();

        _console.Display();

        Assert.That(_handler.LastBackgroundRgb, Is.All.EqualTo(IConsoleHandler.PaletteColor));
    }

    [Test]
    public void Draw_OverAnRgbCell_ShowsThePaletteColourAgain()
    {
        _console.TrueColor = true;
        _console.Initialize();
        _console.DrawRgb(0, 0, ' ', 0x102030, 0x405060);

        _console.Draw(0, 0, 'x', ConsoleColor.Red, ConsoleColor.Blue);
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.LastForegroundRgb[0], Is.EqualTo(IConsoleHandler.PaletteColor));
            Assert.That(_handler.LastBackgroundRgb[0], Is.EqualTo(IConsoleHandler.PaletteColor));
            Assert.That(_handler.PixelAt(0, 0).BackgroundColor, Is.EqualTo(ConsoleColor.Blue));
        });
    }

    [Test]
    public void Clear_TrueColorActive_ResetsRgbCellsToThePalette()
    {
        _console.TrueColor = true;
        _console.Initialize();
        _console.DrawRgb(3, 1, ' ', 0x102030, 0x405060);

        _console.Clear();
        _console.Display();

        Assert.That(_handler.LastBackgroundRgb, Is.All.EqualTo(IConsoleHandler.PaletteColor));
    }

    [Test]
    public void DrawRgb_ConsoleWithoutTrueColor_DrawsTheNearestPaletteColour()
    {
        _handler.SupportsTrueColor = false;
        _console.TrueColor = true;
        _console.Initialize();

        _console.DrawRgb(0, 0, ' ', 0xF0F0F0, 0xC01020); // near White and DarkRed in the default colours
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(_console.TrueColorActive, Is.False);
            Assert.That(_handler.LastForegroundRgb, Is.Empty);
            Assert.That(_handler.PixelAt(0, 0).ForegroundColor, Is.EqualTo(ConsoleColor.White));
            Assert.That(_handler.PixelAt(0, 0).BackgroundColor, Is.EqualTo(ConsoleColor.DarkRed));
        });
    }

    [Test]
    public void DrawRgb_WithPalette_DrawsTheNearestColourOfThatPalette()
    {
        var palette = new uint[16];
        palette[(int)ConsoleColor.Cyan] = 0x804020;
        _console.Palette = palette;
        _console.Initialize();

        _console.DrawRgb(0, 0, ' ', 0, 0x7F4121);
        _console.Display();

        Assert.That(_handler.PixelAt(0, 0).BackgroundColor, Is.EqualTo(ConsoleColor.Cyan));
    }

    [Test]
    public void TrueColor_TurnedOnWhileRunning_AsksTheConsoleAndRendersRgb()
    {
        _console.Initialize();

        _console.TrueColor = true;
        _console.DrawRgb(0, 0, ' ', 0, 0x123456);
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.TrueColorRequested, Is.True);
            Assert.That(_handler.LastBackgroundRgb[0], Is.EqualTo(0x123456));
        });
    }

    [Test]
    public void TrueColor_TurnedOffWhileRunning_RendersPaletteFrames()
    {
        _console.TrueColor = true;
        _console.Initialize();
        _console.Display();

        _console.TrueColor = false;
        _console.DrawRgb(0, 0, ' ', 0, 0xF2F2F2);
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.TrueColorRequested, Is.False);
            Assert.That(_console.TrueColorActive, Is.False);
            Assert.That(_handler.PixelAt(0, 0).BackgroundColor, Is.EqualTo(ConsoleColor.White));
        });
    }

    [Test]
    public void DrawRgb_OutsideTheScreen_IsIgnored()
    {
        _console.TrueColor = true;
        _console.Initialize();

        Assert.DoesNotThrow(() =>
        {
            _console.DrawRgb(-1, 0, ' ', 0, 0);
            _console.DrawRgb(Width, 0, ' ', 0, 0);
            _console.DrawRgb(0, Height, ' ', 0, 0);
        });
    }

    [Test]
    public void DrawRgb_TrueColorActive_DoesNotAllocate()
    {
        _console.TrueColor = true;
        _console.Initialize();
        _console.DrawRgb(0, 0, ' ', 0, 1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            _console.DrawRgb(x, y, ' ', 0, (uint)(x * y));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }
}
