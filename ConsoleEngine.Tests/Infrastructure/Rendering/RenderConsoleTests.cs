using System;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Tests.Fakes;
using CommunityToolkit.HighPerformance;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Infrastructure.Rendering;

public class RenderConsoleTests
{
    private const int Width = 10;
    private const int Height = 5;

    private FakeConsoleHandler _handler;
    private RenderConsole _console;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeConsoleHandler(Width, Height);
        _console = new RenderConsole(_handler);
    }

    [Test]
    public void Ctor_Always_SetsSizeAndDefaultFont()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_console.Width, Is.EqualTo(Width));
            Assert.That(_console.Height, Is.EqualTo(Height));
            Assert.That(_console.Area, Is.EqualTo(Width * Height));
            Assert.That(_console.ScreenCenter, Is.EqualTo(new Vector2(5f, 2.5f)));
            Assert.That(_handler.Font.FontFace, Is.EqualTo("Consolas"));
        });
    }

    [Test]
    public void Initialize_WithPalette_SendsItToTheConsoleAfterInitializing()
    {
        var palette = new uint[16];
        palette[15] = 0x123456;
        _console.Palette = palette;

        _console.Initialize();

        Assert.That(_handler.Initialized, Is.True);
        Assert.That(_handler.Palette, Is.EqualTo(palette));
    }

    [Test]
    public void Initialize_WithoutPalette_LeavesTheConsoleColours()
    {
        _console.Initialize();

        Assert.That(_handler.Palette, Is.Null);
    }

    [Test]
    public void Palette_WithoutSixteenColours_Throws()
    {
        Assert.Throws<ArgumentException>(() => _console.Palette = new uint[8]);
    }

    [Test]
    public void DrawChar_InsideBounds_WritesPixelAtRowMajorIndex()
    {
        _console.Draw(3, 2, 'X', ConsoleColor.Red, ConsoleColor.Blue);
        _console.Display();

        var pixel = _handler.LastFrame[2 * Width + 3];
        Assert.Multiple(() =>
        {
            Assert.That(pixel.Char, Is.EqualTo('X'));
            Assert.That(pixel.ForegroundColor, Is.EqualTo(ConsoleColor.Red));
            Assert.That(pixel.BackgroundColor, Is.EqualTo(ConsoleColor.Blue));
        });
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(Width, 0)]
    [TestCase(0, Height)]
    public void DrawChar_OutsideBounds_IsIgnored(int x, int y)
    {
        Assert.DoesNotThrow(() => _console.Draw(x, y, 'X'));
        Assert.That(_console.GetCharAt(x, y), Is.Null);
    }

    [Test]
    public void DrawString_CrossingRightEdge_ClipsOverflow()
    {
        _console.Draw(Width - 2, 0, "abcd");

        Assert.Multiple(() =>
        {
            Assert.That(_console.GetCharAt(Width - 2, 0), Is.EqualTo('a'));
            Assert.That(_console.GetCharAt(Width - 1, 0), Is.EqualTo('b'));
            Assert.That(_console.GetCharAt(0, 1), Is.EqualTo('\0'));
        });
    }

    [Test]
    public void DrawSprite_AtOffset_UsesXFirstIndexing()
    {
        var sprite = Sprite.FromStringArray(["ab", "cd"]);

        _console.Draw(4, 1, sprite);

        Assert.Multiple(() =>
        {
            Assert.That(_console.GetCharAt(4, 1), Is.EqualTo('a'));
            Assert.That(_console.GetCharAt(5, 1), Is.EqualTo('b'));
            Assert.That(_console.GetCharAt(4, 2), Is.EqualTo('c'));
            Assert.That(_console.GetCharAt(5, 2), Is.EqualTo('d'));
        });
    }

    [Test]
    public void DrawCharSpan_AtOffset_UsesXFirstIndexing()
    {
        var data = new char[,] { { 'a', 'c' }, { 'b', 'd' } };

        _console.Draw(0, 0, new Span2D<char>(data));

        Assert.Multiple(() =>
        {
            Assert.That(_console.GetCharAt(0, 0), Is.EqualTo('a'));
            Assert.That(_console.GetCharAt(1, 0), Is.EqualTo('b'));
            Assert.That(_console.GetCharAt(0, 1), Is.EqualTo('c'));
            Assert.That(_console.GetCharAt(1, 1), Is.EqualTo('d'));
        });
    }

    [Test]
    public void DrawRows_InsideBounds_WritesEachRow()
    {
        _console.Draw(1, 1, new[] { "ab", "cd" }.AsSpan());

        Assert.Multiple(() =>
        {
            Assert.That(_console.GetCharAt(1, 1), Is.EqualTo('a'));
            Assert.That(_console.GetCharAt(2, 1), Is.EqualTo('b'));
            Assert.That(_console.GetCharAt(1, 2), Is.EqualTo('c'));
            Assert.That(_console.GetCharAt(2, 2), Is.EqualTo('d'));
        });
    }

    [TestCase(0, 0, 4, 0)]
    [TestCase(4, 0, 0, 0)]
    [TestCase(2, 0, 2, 4)]
    [TestCase(2, 4, 2, 0)]
    public void DrawLine_Straight_FillsEveryCellBetweenEndpoints(int x1, int y1, int x2, int y2)
    {
        _console.DrawLine(x1, y1, x2, y2, '#');

        for (var x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
        for (var y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            Assert.That(_console.GetCharAt(x, y), Is.EqualTo('#'), $"({x}, {y})");
    }

    [TestCase(0, 0, 4, 4)]
    [TestCase(4, 4, 0, 0)]
    [TestCase(0, 4, 4, 0)]
    public void DrawLine_Diagonal_FillsDiagonalCells(int x1, int y1, int x2, int y2)
    {
        _console.DrawLine(x1, y1, x2, y2, '#');

        var stepX = Math.Sign(x2 - x1);
        var stepY = Math.Sign(y2 - y1);
        for (var i = 0; i <= 4; i++)
            Assert.That(_console.GetCharAt(x1 + i * stepX, y1 + i * stepY), Is.EqualTo('#'));
    }

    [Test]
    public void Clear_WithClearColor_FillsBufferWithBlankInThatColor()
    {
        _console.Draw(0, 0, 'X');
        _console.ClearColor = ConsoleColor.DarkGreen;

        _console.Clear();
        _console.Display();

        Assert.That(_handler.LastFrame, Has.All.Matches<Pixel>(
            p => p.Char == ' ' && p.BackgroundColor == ConsoleColor.DarkGreen));
    }

    [Test]
    public void Initialize_NotResizeable_DisablesResizeAndHidesCursor()
    {
        _console.Initialize();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.Initialized, Is.True);
            Assert.That(_handler.IsResizable, Is.False);
            Assert.That(_handler.CursorVisible, Is.False);
        });
    }

    [Test]
    public void Initialize_Resizeable_LeavesResizeUntouched()
    {
        var console = new RenderConsole(_handler) { Resizeable = true, HideCursor = false };

        console.Initialize();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.IsResizable, Is.Null);
            Assert.That(_handler.CursorVisible, Is.True);
        });
    }

    [Test]
    public void SetTitle_Always_ForwardsToHandler()
    {
        _console.SetTitle("Snake");

        Assert.That(_handler.Title, Is.EqualTo("Snake"));
    }
}
