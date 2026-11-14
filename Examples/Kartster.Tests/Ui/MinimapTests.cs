using System;
using System.IO;
using System.Numerics;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

public class MinimapTests
{
    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static readonly Lazy<RaceMarkers> Markers =
        new(() => RaceMarkers.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static Minimap Map() => TrackScene.CreateMinimap(StarCircuit.Value, Markers.Value);

    [Test]
    public void FromTrack_StarCircuit_FitsTheBoxInsideThePadding()
    {
        var map = Map();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (var i = 0; i < map.LineX.Length; i++)
        {
            minX = Math.Min(minX, map.LineX[i]);
            maxX = Math.Max(maxX, map.LineX[i]);
            minY = Math.Min(minY, map.LineY[i]);
            maxY = Math.Max(maxY, map.LineY[i]);
        }

        Assert.That(minX, Is.GreaterThanOrEqualTo(map.Left + Minimap.Padding));
        Assert.That(maxX, Is.LessThanOrEqualTo(map.Left + map.Size - 1 - Minimap.Padding));
        Assert.That(minY, Is.GreaterThanOrEqualTo(map.Top + Minimap.Padding));
        Assert.That(maxY, Is.LessThanOrEqualTo(map.Top + map.Size - 1 - Minimap.Padding));
        Assert.That(Math.Max(maxX - minX, maxY - minY), Is.GreaterThanOrEqualTo(map.Size - 2 * Minimap.Padding - 2),
            "scaled to fill the box along the longer side");
    }

    [Test]
    public void ToMap_SquareTrack_IsNorthUpAndCentred()
    {
        Vector2[] square = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];
        var map = new Minimap(square, closed: true, new Vector2(50, 0), Vector2.UnitX, left: 10, top: 20, size: 41);

        Assert.That(map.ToMap(new Vector2(0, 0)), Is.EqualTo((10 + Minimap.Padding, 20 + Minimap.Padding)));
        Assert.That(map.ToMap(new Vector2(100, 100)), Is.EqualTo((10 + 40 - Minimap.Padding, 20 + 40 - Minimap.Padding)));
        Assert.That(map.ToMap(new Vector2(50, 50)), Is.EqualTo((30, 40)));
    }

    [Test]
    public void Draw_PlayerAndBots_ArrowWhereThePlayerIsAndDotsInTheirColours()
    {
        var map = Map();
        var preview = new UiPreview();
        var (player, _) = StarCircuit.Value.Sample(100f);
        var (bot, _) = StarCircuit.Value.Sample(400f);
        Vector2[] positions = [new(player.X, player.Z), new(bot.X, bot.Z)];
        uint[] colors = [0, 0xB06A30];

        map.Draw(preview.Target, positions, colors, playerIndex: 0, playerRadians: 0f);
        preview.Save("minimap");

        var (px, py) = map.ToMap(positions[0]);
        Assert.That(preview.Bounds(UiColors.Selected).Value, Is.EqualTo((px - 2, py - 3, px + 2, py + 2)),
            "facing north: tip 3 px up, back corners 2 px down and 2 px to the sides");
        var (bx, by) = map.ToMap(positions[1]);
        Assert.That(preview[bx, by], Is.EqualTo(0xB06A30u));
        var line = preview.Bounds(UiColors.Text).Value;
        Assert.That(line.Left, Is.GreaterThanOrEqualTo(map.Left));
        Assert.That(line.Right, Is.LessThan(map.Left + map.Size), "nothing past the box");
    }

    [Test]
    public void Draw_EveryFrame_DoesNotAllocate()
    {
        var map = Map();
        var preview = new UiPreview();
        var positions = new Vector2[6];
        var colors = new uint[6];
        map.Draw(preview.Target, positions, colors, 0, 0f);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 100; frame++)
            map.Draw(preview.Target, positions, colors, 0, frame * 0.1f);

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
