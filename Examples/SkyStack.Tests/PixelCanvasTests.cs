using System;
using ConsoleEngine.Infrastructure.Rendering;
using NUnit.Framework;
using SkyStack.Rendering;
using SkyStack.Tests.Fakes;

namespace SkyStack.Tests;

public class PixelCanvasTests
{
    [Test]
    public void Ctor_OddHeight_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PixelCanvas(4, 3));

    [Test]
    public void Set_OutsideTheCanvas_IsIgnored()
    {
        var canvas = new PixelCanvas(4, 4);

        canvas.Set(-1, 0, ConsoleColor.Red);
        canvas.Set(4, 4, ConsoleColor.Red);

        Assert.That(canvas.Get(-1, 0), Is.EqualTo(ConsoleColor.Black));
        Assert.That(canvas.Get(0, 0), Is.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void FillRect_PartlyOutside_FillsOnlyTheInside()
    {
        var canvas = new PixelCanvas(4, 4);

        canvas.FillRect(-2, -2, 4, 4, ConsoleColor.Green);

        Assert.That(canvas.Get(1, 1), Is.EqualTo(ConsoleColor.Green));
        Assert.That(canvas.Get(2, 1), Is.EqualTo(ConsoleColor.Black));
        Assert.That(canvas.Get(1, 2), Is.EqualTo(ConsoleColor.Black));
    }

    [Test]
    public void Present_TwoPixelRows_FillOneConsoleRowWithHalfBlocks()
    {
        var console = new RenderConsole(new FakeConsoleHandler(4, 2));
        var canvas = new PixelCanvas(4, 4);

        canvas.Present(console);

        Assert.That(console.GetCharAt(0, 0), Is.EqualTo(PixelCanvas.HalfBlock));
        Assert.That(console.GetCharAt(3, 1), Is.EqualTo(PixelCanvas.HalfBlock));
    }

    [Test]
    public void Draw_Sprite_UsesThePaletteAndSkipsClearPixels()
    {
        var sprite = PixelSprite.FromRows("ab", "a.", ".b");
        var canvas = new PixelCanvas(4, 4);
        canvas.Fill(ConsoleColor.Blue);

        canvas.Draw(sprite, 1, 1, [ConsoleColor.Red, ConsoleColor.Yellow]);

        Assert.That(canvas.Get(1, 1), Is.EqualTo(ConsoleColor.Red));
        Assert.That(canvas.Get(2, 1), Is.EqualTo(ConsoleColor.Blue));
        Assert.That(canvas.Get(2, 2), Is.EqualTo(ConsoleColor.Yellow));
    }

    [Test]
    public void Draw_Mirrored_FlipsLeftAndRight()
    {
        var sprite = PixelSprite.FromRows("a", "a.");
        var canvas = new PixelCanvas(2, 2);

        canvas.Draw(sprite, 0, 0, [ConsoleColor.Red], mirrored: true);

        Assert.That(canvas.Get(0, 0), Is.EqualTo(ConsoleColor.Black));
        Assert.That(canvas.Get(1, 0), Is.EqualTo(ConsoleColor.Red));
    }

    [Test]
    public void FromRows_UnknownKey_Throws() =>
        Assert.Throws<ArgumentException>(() => PixelSprite.FromRows("a", "ax"));

    [Test]
    public void FromRows_RowsOfDifferentWidth_Throws() =>
        Assert.Throws<ArgumentException>(() => PixelSprite.FromRows("a", "aa", "a"));

    [Test]
    public void ClimberSprites_AllHaveTheBodySize()
    {
        PixelSprite[] sprites =
        [
            ClimberLooks.Standing, ClimberLooks.RunningA, ClimberLooks.RunningB, ClimberLooks.Rising,
            ClimberLooks.Falling
        ];

        foreach (var sprite in sprites)
        {
            Assert.That(sprite.Width, Is.EqualTo(Climbing.ClimbPhysics.BodyWidth));
            Assert.That(sprite.Height, Is.EqualTo(Climbing.ClimbPhysics.BodyHeight));
        }
    }

    [Test]
    public void DrawNumber_PixelFont_PaintsGlyphPixels()
    {
        var canvas = new PixelCanvas(8, 6);

        PixelFont.DrawNumber(canvas, 0, 0, 10, ConsoleColor.White);

        Assert.That(PixelFont.MeasureNumber(10), Is.EqualTo(7));
        Assert.That(canvas.Get(1, 0), Is.EqualTo(ConsoleColor.White)); // top of the 1
        Assert.That(canvas.Get(5, 2), Is.EqualTo(ConsoleColor.Black)); // hole of the 0
        Assert.That(canvas.Get(4, 2), Is.EqualTo(ConsoleColor.White));
    }
}
