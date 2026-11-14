using System;
using ConsoleEngine.Rendering3D;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

public class HudTests
{
    private const int Width = UiPreview.Width;
    private const int Height = UiPreview.Height;

    private readonly Hud _hud = new();

    private static HudState Race() => new()
    {
        SpeedKmh = 81, Lap = 2, LapCount = 3, RaceSeconds = 655.1f, Position = 2, KartCount = 3,
        Standings =
        [
            new HudStanding { Position = 1, Name = "BIG ENDIAN", Seconds = 322.4f },
            new HudStanding { Position = 2, Name = "MACH ZEN", Seconds = 508f, IsPlayer = true },
            new HudStanding { Position = 3, Name = "PUNKSTER", Seconds = 521.9f }
        ],
        StandingCount = 3,
        PowerUp = "HOMING JOJKA"
    };

    [Test]
    public void Draw_LapLine_IsBottomLeftAtScaleTwo()
    {
        var preview = new UiPreview();
        _hud.Draw(preview.Target, Race());
        preview.Save("hud-race");

        // "LAP 2/3" in white, the clock after it in sky blue.
        var lapTop = Height - Hud.Margin - 10;
        var lap = preview.Bounds(UiColors.Text, 0, lapTop, Width / 2, Height).Value;
        Assert.That(lap, Is.EqualTo((Hud.Margin, lapTop, Hud.Margin + PixelFont.MeasureText("LAP 2/3", 2) - 1, lapTop + 9)));
        var clock = preview.Bounds(UiColors.Sky, 0, lapTop, Width / 2, Height).Value;
        Assert.That(clock.Left, Is.EqualTo(lap.Right + 1 + 2 + 4)); // the font's 2-px gap, then 4 px
        Assert.That(clock.Right - clock.Left + 1, Is.EqualTo(PixelFont.MeasureText("10:55.1")));
        Assert.That(clock.Bottom, Is.EqualTo(lap.Bottom));
    }

    [Test]
    public void Draw_Standings_SitOnTheLapLineWithThePlayerInYellow()
    {
        var preview = new UiPreview();
        _hud.Draw(preview.Target, Race());

        var player = preview.Bounds(UiColors.Selected, 0, 0, Width / 2, Height).Value;
        var lapTop = Height - Hud.Margin - 10;
        var rowTop = lapTop - Hud.Margin - 2 * Hud.StandingRowHeight + 2;
        Assert.That(player.Top, Is.EqualTo(rowTop));
        Assert.That(player.Bottom, Is.EqualTo(rowTop + 4));
        Assert.That(player.Left, Is.EqualTo(Hud.Margin));
    }

    [Test]
    public void Draw_OneLapRace_ShowsOnlyTheClock()
    {
        var preview = new UiPreview();
        var state = Race();
        state.LapCount = 1;
        _hud.Draw(preview.Target, state);

        var lapTop = Height - Hud.Margin - 10;
        var clock = preview.Bounds(UiColors.Text, 0, lapTop, Width / 2, Height).Value;
        Assert.That(clock.Right - clock.Left + 1, Is.EqualTo(PixelFont.MeasureText("10:55.1", 2)));
    }

    [Test]
    public void Draw_NoStandings_ShowsPosition()
    {
        var preview = new UiPreview();
        var state = Race();
        state.StandingCount = 0;
        _hud.Draw(preview.Target, state);

        var lapTop = Height - Hud.Margin - 10;
        var position = preview.Bounds(UiColors.Text, 0, 0, Width / 2, lapTop - 1).Value;
        Assert.That(position.Right - position.Left + 1, Is.EqualTo(PixelFont.MeasureText("POS 2/3")));
    }

    [Test]
    public void Draw_Speedometer_IsBottomRightAndNeedleFollowsSpeed()
    {
        var stopped = new UiPreview();
        var flatOut = new UiPreview();
        var state = new HudState { SpeedKmh = 0 };
        _hud.Draw(stopped.Target, state);
        state.SpeedKmh = 500; // pins at the top of the dial
        _hud.Draw(flatOut.Target, state);

        var centerX = Width - Hud.Margin - Hud.DialRadius;
        var slow = stopped.Bounds(UiColors.Selected).Value;
        var fast = flatOut.Bounds(UiColors.Selected).Value;
        Assert.That(slow.Right, Is.LessThan(centerX), "needle at 0 points down-left");
        Assert.That(fast.Left, Is.GreaterThan(centerX), "needle pinned points down-right");
        Assert.That(stopped.Bounds(UiColors.Redline).Value.Left, Is.GreaterThan(centerX));
        Assert.That(stopped.Bounds(UiColors.Sky).Value.Bottom, Is.LessThan(Height)); // "KM/H"
    }

    [Test]
    public void Draw_PowerUp_IsTopRightAndNothingWhenEmpty()
    {
        var held = new UiPreview();
        _hud.Draw(held.Target, Race());
        var name = held.Bounds(UiColors.Text, Width / 2, 0, Width, 40).Value;
        Assert.That(name.Right, Is.EqualTo(Width - Hud.Margin - 1));
        Assert.That(held.Bounds(UiColors.Panel).Value.Top, Is.EqualTo(Hud.Margin + 1));

        var empty = new UiPreview();
        var state = Race();
        state.PowerUp = null;
        _hud.Draw(empty.Target, state);
        Assert.That(empty.Count(UiColors.Panel), Is.Zero);
    }

    // Ink widths: the glyphs' own pixels, so "!" (one column wide) ends a column early.
    [TestCase(3, false, false, "3", 8, 24)]
    [TestCase(0, true, false, "GO!", 8, 11 * 8 - 8)]
    [TestCase(0, false, true, "FINISHED", 3, 31 * 3)]
    public void Draw_Announcement_IsTopCentre(int countdown, bool go, bool finished, string text, int scale, int inkWidth)
    {
        var preview = new UiPreview();
        _hud.Draw(preview.Target, new HudState { Countdown = countdown, ShowGo = go, Finished = finished });

        var announcement = preview.Bounds(UiColors.Selected, 0, 0, Width, 100).Value;
        Assert.That(announcement.Right - announcement.Left + 1, Is.EqualTo(inkWidth));
        Assert.That(announcement.Left, Is.EqualTo(Width / 2 - PixelFont.MeasureText(text, scale) / 2));
    }

    [Test]
    public void Draw_IntoPaletteFrame_DoesNotThrow()
    {
        var frame = new ConsoleColor[Width * Height];
        var target = new PaletteTarget(frame, Width, Height, ConsolePalette.Campbell);
        Assert.DoesNotThrow(() => _hud.Draw(target, Race()));
        Assert.That(frame, Has.Some.EqualTo(ConsoleColor.White));
    }

    [Test]
    public void Draw_EveryFrame_DoesNotAllocate()
    {
        var preview = new UiPreview();
        var state = Race();
        state.Countdown = 2;
        _hud.Draw(preview.Target, state); // warm up

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 10; frame++)
        {
            state.RaceSeconds += 0.016f;
            _hud.Draw(preview.Target, state);
        }

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
