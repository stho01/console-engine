using System;
using ConsoleEngine.Rendering3D;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

public class PixelTargetTests
{
    [Test]
    public void PaletteTarget_Set_WritesNearestPaletteColour()
    {
        var frame = new ConsoleColor[4 * 2];
        var target = new PaletteTarget(frame, 4, 2, ConsolePalette.Campbell);

        target.Set(0, 0, 0xFF0000); // nearest: 0xC50F1F (DarkRed), not the pinker 0xE74856 (Red)
        target.Set(1, 0, 0x000000); // 0x0C0C0C (Black)
        target.Set(2, 0, 0xFFFFFF); // 0xF2F2F2 (White)
        target.Set(3, 1, 0x0030D0); // 0x0037DA (DarkBlue)

        Assert.That(frame[0], Is.EqualTo(ConsoleColor.DarkRed));
        Assert.That(frame[1], Is.EqualTo(ConsoleColor.Black));
        Assert.That(frame[2], Is.EqualTo(ConsoleColor.White));
        Assert.That(frame[7], Is.EqualTo(ConsoleColor.DarkBlue));
    }

    [Test]
    public void PaletteTarget_SameColourEverywhere_HasNoDither()
    {
        var frame = new ConsoleColor[8 * 8];
        var target = new PaletteTarget(frame, 8, 8, ConsolePalette.Campbell);
        Shapes.FillRect(target, 0, 0, 8, 8, 0x808080);

        Assert.That(frame, Is.All.EqualTo(frame[0]));
    }

    [Test]
    public void Targets_OutOfRange_AreIgnored()
    {
        var palette = new PaletteTarget(new ConsoleColor[4], 2, 2, ConsolePalette.Campbell);
        var rgb = new RgbTarget(new uint[4], 2, 2);
        foreach (IPixelTarget target in new IPixelTarget[] { palette, rgb })
        {
            Assert.DoesNotThrow(() =>
            {
                target.Set(-1, 0, 0xFFFFFF);
                target.Set(0, -1, 0xFFFFFF);
                target.Set(2, 0, 0xFFFFFF);
                target.Set(0, 2, 0xFFFFFF);
            });
        }

        Assert.That(rgb.Frame, Is.All.Zero);
    }

    [Test]
    public void RgbTarget_Set_WritesColour()
    {
        var target = new RgbTarget(new uint[6], 3, 2);
        target.Set(2, 1, 0xFFABCDEF);
        Assert.That(target.Frame[5], Is.EqualTo(0xABCDEF));
    }

    [Test]
    public void Shapes_FrameAndLine_DrawExpectedPixels()
    {
        var preview = new UiPreview();
        Shapes.Frame(preview.Target, 10, 10, 5, 4, 0xFFFFFF);
        Assert.That(preview.Count(0xFFFFFF), Is.EqualTo(2 * 5 + 2 * 2));

        Shapes.Line(preview.Target, 0, 50, 9, 59, 0x00FF00);
        Assert.That(preview.Count(0x00FF00), Is.EqualTo(10));
        Assert.That(preview[9, 59], Is.EqualTo(0x00FF00));

        Shapes.FillRect(preview.Target, -10, -10, 1000, 1000, 0x0000FF); // clipped, no throw
        Assert.That(preview.Count(0x0000FF), Is.EqualTo(UiPreview.Width * UiPreview.Height));
    }

    [Test]
    public void Shapes_Lerp_BlendsChannels()
    {
        Assert.That(Shapes.Lerp(0x000000, 0xFF8040, 0f), Is.EqualTo(0x000000));
        Assert.That(Shapes.Lerp(0x000000, 0xFF8040, 1f), Is.EqualTo(0xFF8040));
        Assert.That(Shapes.Lerp(0x000000, 0xFF8040, 0.5f), Is.EqualTo(0x804020));
    }
}
