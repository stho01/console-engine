using System;
using ConsoleEngine.Infrastructure.Rendering;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Infrastructure.Rendering;

public class SpriteTests
{
    [Test]
    public void FromStringArray_RowsOfDifferentLength_PadsToWidestRow()
    {
        var sprite = Sprite.FromStringArray(["abc", "d"]);

        Assert.Multiple(() =>
        {
            Assert.That(sprite.Width, Is.EqualTo(3));
            Assert.That(sprite.Height, Is.EqualTo(2));
            Assert.That(sprite.Data[0, 1].Char, Is.EqualTo('d'));
            Assert.That(sprite.Data[2, 1].Char, Is.EqualTo(' '));
        });
    }

    [Test]
    public void FromStringArray_WithColors_AppliesColorsToEveryPixel()
    {
        var sprite = Sprite.FromStringArray(["ab"], ConsoleColor.Green, ConsoleColor.DarkBlue);

        Assert.That(sprite.Data, Has.All.Matches<Abstractions.Rendering.Pixel>(
            p => p.ForegroundColor == ConsoleColor.Green && p.BackgroundColor == ConsoleColor.DarkBlue));
    }

    [Test]
    public void FromString_MultiLine_SplitsOnEnvironmentNewLine()
    {
        var sprite = Sprite.FromString($"ab{Environment.NewLine}cd");

        Assert.Multiple(() =>
        {
            Assert.That(sprite.Width, Is.EqualTo(2));
            Assert.That(sprite.Height, Is.EqualTo(2));
            Assert.That(sprite.Data[1, 1].Char, Is.EqualTo('d'));
        });
    }

    [Test]
    public void FromCharArray_Always_KeepsXFirstLayout()
    {
        var sprite = Sprite.FromCharArray(new char[,] { { 'a', 'b', 'c' } });

        Assert.Multiple(() =>
        {
            Assert.That(sprite.Width, Is.EqualTo(1));
            Assert.That(sprite.Height, Is.EqualTo(3));
            Assert.That(sprite.Data[0, 2].Char, Is.EqualTo('c'));
        });
    }

    [Test]
    public void Ctor_OddSize_SetsOriginToFlooredCenter()
    {
        var sprite = Sprite.FromStringArray(["abc", "def", "ghi"]);

        Assert.That(sprite.Origin, Is.EqualTo(new System.Numerics.Vector2(1, 1)));
    }

    [Test]
    public void Draw_String_OverwritesPixelsInRow()
    {
        var sprite = Sprite.FromStringArray(["....", "...."]);

        sprite.Draw(1, 1, "xy", ConsoleColor.Red, ConsoleColor.Black);

        Assert.Multiple(() =>
        {
            Assert.That(sprite.Data[1, 1].Char, Is.EqualTo('x'));
            Assert.That(sprite.Data[2, 1].Char, Is.EqualTo('y'));
            Assert.That(sprite.Data[2, 1].ForegroundColor, Is.EqualTo(ConsoleColor.Red));
            Assert.That(sprite.Data[1, 0].Char, Is.EqualTo('.'));
        });
    }
}
