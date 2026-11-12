using System;
using ConsoleEngine.Assets;
using ConsoleEngine.Infrastructure.Rendering;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Assets;

public class SpriteLoaderTests
{
    private static Sprite Load(string text) =>
        new AssetManager(new MemoryAssetSource().Add("Sprites/test.sprite", text)).Load<Sprite>("Sprites/test.sprite");

    private static string Row(Sprite sprite, int y)
    {
        var chars = new char[sprite.Width];
        for (var x = 0; x < sprite.Width; x++)
            chars[x] = sprite.Data[x, y].Char;

        return new string(chars);
    }

    [Test]
    public void Load_HeaderAndRows_UsesColorsAndKeepsSpaces()
    {
        var sprite = Load("!fg: Yellow\n!BG: darkblue\n---\n /\\_/\\\n( o.o )\n");

        Assert.Multiple(() =>
        {
            Assert.That(sprite.Width, Is.EqualTo(7));
            Assert.That(sprite.Height, Is.EqualTo(2));
            Assert.That(Row(sprite, 0), Is.EqualTo(" /\\_/\\ "));
            Assert.That(Row(sprite, 1), Is.EqualTo("( o.o )"));
            Assert.That(sprite.Data[0, 0].ForegroundColor, Is.EqualTo(ConsoleColor.Yellow));
            Assert.That(sprite.Data[0, 0].BackgroundColor, Is.EqualTo(ConsoleColor.DarkBlue));
        });
    }

    [Test]
    public void Load_NoHeader_WholeFileIsRowsInWhiteOnBlack()
    {
        var sprite = Load("ab\r\nc\r\n");

        Assert.Multiple(() =>
        {
            Assert.That(Row(sprite, 0), Is.EqualTo("ab"));
            Assert.That(Row(sprite, 1), Is.EqualTo("c "));
            Assert.That(sprite.Data[1, 1].ForegroundColor, Is.EqualTo(ConsoleColor.White));
            Assert.That(sprite.Data[1, 1].BackgroundColor, Is.EqualTo(ConsoleColor.Black));
        });
    }

    [Test]
    public void Load_EmptyHeader_RowsMayStartWithBang()
    {
        var sprite = Load("---\n!!\n");

        Assert.That(Row(sprite, 0), Is.EqualTo("!!"));
    }

    [Test]
    public void Load_SeparatorLikeRowAfterHeader_IsARow()
    {
        var sprite = Load("!fg: Red\n---\n---\n");

        Assert.That(Row(sprite, 0), Is.EqualTo("---"));
    }

    [Test]
    public void Load_BoxDrawingCharacters_SurviveUtf8()
    {
        Assert.That(Row(Load("╔═╗\n"), 0), Is.EqualTo("╔═╗"));
    }

    [TestCase("!fg: Yellow\n!bg: Red\n", "The header has no '---' line to end it.")]
    [TestCase("!fg: Yellow\n---\n", "The sprite has no rows.")]
    [TestCase("", "The sprite has no rows.")]
    [TestCase("!fg: Purple\n---\nx", "Line 1: 'Purple' is not a ConsoleColor.")]
    [TestCase("!fg: 99\n---\nx", "Line 1: '99' is not a ConsoleColor.")]
    [TestCase("!fg: Red\n!size: 3\n---\nx", "Line 2: unknown header key 'size'. Known keys: fg, bg.")]
    [TestCase("!fg: Red\nfg Red\n---\nx", "Line 2: expected '!key: value' in the header, got 'fg Red'.")]
    public void Load_BadFile_ThrowsFormatExceptionWithLine(string text, string message)
    {
        var e = Assert.Throws<AssetLoadException>(() => Load(text));

        Assert.Multiple(() =>
        {
            Assert.That(e.InnerException, Is.TypeOf<FormatException>());
            Assert.That(e.InnerException?.Message, Is.EqualTo(message));
        });
    }

    [Test]
    public void Clone_DrawIntoCopy_LeavesOriginalUnchanged()
    {
        var original = Load("###\n");
        original.Origin = new System.Numerics.Vector2(2, 0);

        var copy = original.Clone();
        copy.Draw(0, 0, "Bob", ConsoleColor.Red, ConsoleColor.Black);

        Assert.Multiple(() =>
        {
            Assert.That(Row(original, 0), Is.EqualTo("###"));
            Assert.That(Row(copy, 0), Is.EqualTo("Bob"));
            Assert.That(copy.Origin, Is.EqualTo(original.Origin));
        });
    }
}
