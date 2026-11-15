using System;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Native;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Native;

public class VtFrameTests
{
    private const int Width = 3;
    private const int Height = 2;
    private const string Esc = "\u001b";

    private VtFrame _frame;
    private Pixel[] _pixels;
    private uint[] _foreground;
    private uint[] _background;

    [SetUp]
    public void SetUp()
    {
        _frame = new VtFrame(Width, Height);
        _pixels = new Pixel[Width * Height];
        _foreground = new uint[Width * Height];
        _background = new uint[Width * Height];
        Array.Fill(_pixels, new Pixel { Char = ' ' });
    }

    private string Build()
    {
        _frame.Build(_pixels, _foreground, _background);
        return _frame.Text.ToString();
    }

    [Test]
    public void Build_FirstFrameOfOneColour_MovesToEachRowAndSendsTheColourOnce()
    {
        Array.Fill(_background, 0x0A141Eu);

        var text = Build();

        Assert.That(text, Is.EqualTo($"{Esc}[1;1H{Esc}[38;2;0;0;0;48;2;10;20;30m   {Esc}[2;1H   "));
    }

    [Test]
    public void Build_UnchangedFrame_ReturnsFalseAndNoText()
    {
        Build();

        var changed = _frame.Build(_pixels, _foreground, _background);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(_frame.Length, Is.Zero);
        });
    }

    [Test]
    public void Build_OneCellChanged_MovesThereAndSendsOnlyTheChangedColour()
    {
        Build();
        _background[1 * Width + 2] = 0xFF8000;
        _pixels[1 * Width + 2].Char = 'k';

        var text = Build();

        Assert.That(text, Is.EqualTo($"{Esc}[2;3H{Esc}[38;2;0;0;0;48;2;255;128;0mk"));
    }

    [Test]
    public void Build_ColourChangesAlongARun_SendsOnlyTheLayerThatChanged()
    {
        Build();
        _foreground[0] = 0x010203;
        _pixels[0].Char = 'a';
        _foreground[1] = 0x010203;
        _background[1] = 0x040506;
        _pixels[1].Char = 'b';

        var text = Build();

        Assert.That(text, Is.EqualTo($"{Esc}[1;1H{Esc}[38;2;1;2;3;48;2;0;0;0ma{Esc}[48;2;4;5;6mb"));
    }

    [Test]
    public void Build_TwoRunsInARow_MovesTheCursorForTheSecond()
    {
        Build();
        _pixels[0].Char = 'a';
        _pixels[2].Char = 'c';

        var text = Build();

        Assert.That(text, Is.EqualTo($"{Esc}[1;1H{Esc}[38;2;0;0;0;48;2;0;0;0ma{Esc}[1;3Hc"));
    }

    [Test]
    public void Build_PaletteColourCell_UsesThePaletteEntry()
    {
        _frame.Palette[(int)ConsoleColor.Red] = 0xE74856;
        Build();
        _pixels[0] = new Pixel { Char = 'r', ForegroundColor = ConsoleColor.Red };
        _foreground[0] = IConsoleHandler.PaletteColor;

        var text = Build();

        Assert.That(text, Is.EqualTo($"{Esc}[1;1H{Esc}[38;2;231;72;86;48;2;0;0;0mr"));
    }

    [Test]
    public void Build_ControlCharacters_AreWrittenAsSpaces()
    {
        Build();
        _pixels[0].Char = '\0';
        _foreground[0] = 1; // changes the cell, the character stays a blank

        var text = Build();

        Assert.That(text, Does.EndWith("m "));
    }

    [Test]
    public void Invalidate_AfterAFrame_WritesEveryCellAgain()
    {
        var first = Build();

        _frame.Invalidate();

        Assert.That(Build(), Is.EqualTo(first));
    }

    [Test]
    public void Build_LargeNumbers_AreWrittenInFull()
    {
        var frame = new VtFrame(120, 101);
        var size = 120 * 101;
        var pixels = new Pixel[size];
        Array.Fill(pixels, new Pixel { Char = ' ' });
        frame.Build(pixels, new uint[size], new uint[size]);
        pixels[size - 1].Char = 'z';

        frame.Build(pixels, new uint[size], new uint[size]);

        Assert.That(frame.Text.ToString(), Is.EqualTo($"{Esc}[101;120H{Esc}[38;2;0;0;0;48;2;0;0;0mz"));
    }

    [Test]
    public void Build_NoiseFrame_FitsTheBufferAndDoesNotAllocate()
    {
        var random = new Random(1);
        Build();
        for (var i = 0; i < _background.Length; i++)
        {
            _foreground[i] = (uint)random.Next(0x1000000);
            _background[i] = (uint)random.Next(0x1000000);
            _pixels[i].Char = i % 2 == 0 ? 'x' : ' ';
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var changed = _frame.Build(_pixels, _foreground, _background);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(allocated, Is.Zero);
        });
    }
}
