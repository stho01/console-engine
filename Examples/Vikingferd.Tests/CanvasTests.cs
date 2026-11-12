using System;
using NUnit.Framework;
using Vikingferd.Rendering;

namespace Vikingferd.Tests;

public class CanvasTests
{
    [Test]
    public void Cell_DifferentPixels_IsUpperHalfWithTopAsForeground()
    {
        var canvas = new Canvas(2, 1);
        canvas.Pixel(0, 0, ConsoleColor.Red);
        canvas.Pixel(0, 1, ConsoleColor.Blue);

        canvas.Cell(0, 0, out var character, out var foreground, out var background);

        Assert.That((character, foreground, background),
            Is.EqualTo((Canvas.UpperHalf, ConsoleColor.Red, ConsoleColor.Blue)));
    }

    [Test]
    public void Cell_SamePixels_IsASpace()
    {
        var canvas = new Canvas(1, 1);
        canvas.Clear(ConsoleColor.Green);

        canvas.Cell(0, 0, out var character, out _, out var background);

        Assert.That((character, background), Is.EqualTo((' ', ConsoleColor.Green)));
    }

    [Test]
    public void Glyph_ShowsOverTheBottomPixel_UntilAPixelIsDrawnInItsCell()
    {
        var canvas = new Canvas(1, 1);
        canvas.Pixel(0, 1, ConsoleColor.DarkBlue);
        canvas.Glyph(0, 0, '*', ConsoleColor.Yellow);

        canvas.Cell(0, 0, out var character, out var foreground, out var background);
        Assert.That((character, foreground, background), Is.EqualTo(('*', ConsoleColor.Yellow, ConsoleColor.DarkBlue)));

        canvas.Pixel(0, 0, ConsoleColor.White);
        canvas.Cell(0, 0, out character, out _, out _);
        Assert.That(character, Is.EqualTo(Canvas.UpperHalf));
    }

    [Test]
    public void Text_HasItsOwnBackgroundAndClipsAtTheEdge()
    {
        var canvas = new Canvas(3, 1);

        canvas.Text(1, 0, "abc", ConsoleColor.White, ConsoleColor.DarkRed);

        canvas.Cell(2, 0, out var character, out _, out var background);
        Assert.That((character, background), Is.EqualTo(('b', ConsoleColor.DarkRed)));
    }

    [Test]
    public void Draw_Sprite_SkipsTransparentPixelsAndMirrors()
    {
        var canvas = new Canvas(3, 1);
        canvas.Clear(ConsoleColor.Black);
        var sprite = PixelSprite.Parse("c.*");

        sprite.Draw(canvas, 0, 0, accent: ConsoleColor.Blue, mirror: true);

        Assert.That(canvas.PixelAt(0, 0), Is.EqualTo(ConsoleColor.Blue), "accent, mirrored to the left");
        Assert.That(canvas.PixelAt(1, 0), Is.EqualTo(ConsoleColor.Black), "transparent");
        Assert.That(canvas.PixelAt(2, 0), Is.EqualTo(ConsoleColor.Red));
    }

    [Test]
    public void Parse_UnknownPixel_Throws() => Assert.Throws<FormatException>(() => PixelSprite.Parse("x"));
}
