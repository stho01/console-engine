using System;
using System.Collections.Generic;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

public class PixelFontTests
{
    private const uint Ink = 0xFFFFFF;

    [Test]
    public void GlyphRows_EveryListedCharacter_HasFifteenCells()
    {
        Assert.That(PixelFont.GlyphRows, Has.Length.EqualTo(PixelFont.Characters.Length));
        foreach (var rows in PixelFont.GlyphRows)
            Assert.That(rows, Has.Length.EqualTo(PixelFont.GlyphWidth * PixelFont.GlyphHeight).And.Match("^[#.]+$"));
    }

    [Test]
    public void HasGlyph_EveryRequiredCharacter_IsTrue()
    {
        const string required = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:/-+%!?()#>";
        foreach (var c in required)
            Assert.That(PixelFont.HasGlyph(c), Is.True, $"'{c}'");
        Assert.That(PixelFont.HasGlyph('@'), Is.False);
        Assert.That(PixelFont.HasGlyph('æ'), Is.False);
    }

    [Test]
    public void Glyphs_LettersAndDigits_AreAllDifferent()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 36; i++)
            Assert.That(seen.Add(PixelFont.GlyphRows[i]), Is.True, $"'{PixelFont.Characters[i]}' repeats a glyph");
    }

    [TestCase('8', 3, 5)]
    [TestCase('1', 3, 5)]
    [TestCase('.', 1, 1)]
    public void DrawText_OneGlyph_FitsInThreeByFive(char c, int width, int height)
    {
        var preview = new UiPreview();
        PixelFont.DrawText(preview.Target, 10, 20, c.ToString(), Ink);

        var (left, top, right, bottom) = preview.Bounds(Ink).Value;
        Assert.That(right - left + 1, Is.EqualTo(width));
        Assert.That(bottom - top + 1, Is.EqualTo(height));
        Assert.That(left, Is.GreaterThanOrEqualTo(10));
        Assert.That(bottom, Is.LessThanOrEqualTo(24));
    }

    [Test]
    public void DrawText_Scaled_DrawsSquaresOfScale()
    {
        var preview = new UiPreview();
        PixelFont.DrawText(preview.Target, 0, 0, "8", Ink, scale: 3);

        Assert.That(preview.Bounds(Ink), Is.EqualTo((0, 0, 8, 14)));
        Assert.That(preview.Count(Ink), Is.EqualTo(13 * 9)); // "8" has 13 font pixels
    }

    [Test]
    public void DrawText_ReturnsNextX()
    {
        var preview = new UiPreview();
        Assert.That(PixelFont.DrawText(preview.Target, 5, 0, "AB", Ink, 2), Is.EqualTo(5 + 2 * 8));
    }

    [Test]
    public void MeasureText_IsCharsTimesAdvanceWithoutLastGap()
    {
        Assert.That(PixelFont.MeasureText(""), Is.Zero);
        Assert.That(PixelFont.MeasureText("A"), Is.EqualTo(3));
        Assert.That(PixelFont.MeasureText("LAP 2/3"), Is.EqualTo(27));
        Assert.That(PixelFont.MeasureText("LAP 2/3", 2), Is.EqualTo(54));
    }

    [Test]
    public void DrawText_LowerCase_DrawsLikeUpperCase()
    {
        var lower = new UiPreview();
        var upper = new UiPreview();
        PixelFont.DrawText(lower.Target, 0, 0, "olabil", Ink);
        PixelFont.DrawText(upper.Target, 0, 0, "OLABIL", Ink);
        Assert.That(lower.Frame, Is.EqualTo(upper.Frame));
    }

    [Test]
    public void DrawText_AtEdges_ClipsWithoutThrowing()
    {
        var preview = new UiPreview();
        PixelFont.DrawTextOutlined(preview.Target, -5, -2, "EDGE", Ink, 0x000001, 2);
        PixelFont.DrawTextOutlined(preview.Target, UiPreview.Width - 6, UiPreview.Height - 4, "EDGE", Ink, 0x000001, 2);

        Assert.That(preview[0, 6], Is.EqualTo(Ink)); // the bottom bar of the first E, cut at x = 0
        Assert.That(preview.Bounds(Ink).Value.Right, Is.EqualTo(UiPreview.Width - 1));
    }

    [Test]
    public void DrawTextOutlined_RingsTheTextInOutlineColour()
    {
        var preview = new UiPreview();
        PixelFont.DrawTextOutlined(preview.Target, 10, 10, "I", Ink, 0x000001);

        Assert.That(preview.Bounds(Ink), Is.EqualTo((10, 10, 12, 14)));
        Assert.That(preview.Bounds(0x000001), Is.EqualTo((9, 9, 13, 15)));
    }

    [Test]
    public void DrawText_FromSpan_DoesNotAllocate()
    {
        var preview = new UiPreview();
        Span<char> text = stackalloc char[] { '1', ':', '2', '3', '.', '4' };
        PixelFont.DrawTextOutlined(preview.Target, 0, 0, text, Ink, 0, 2); // warm up

        var before = GC.GetAllocatedBytesForCurrentThread();
        PixelFont.DrawTextOutlined(preview.Target, 0, 0, text, Ink, 0, 2);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
