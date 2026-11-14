using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Kartster.Race;
using NUnit.Framework;

namespace Kartster.Tests.Race;

public class RaceMarkersTests
{
    [Test]
    public void Parse_AllMarkers_ReadsThemInOrder()
    {
        var markers = RaceMarkers.Parse(
        [
            "# comment",
            "laps 2",
            "",
            "start 1 2 0 -2 3 4",
            "checkpoint 5 6 1 0 7 8",
            "checkpoint -5 -6 0 1 1 1",
            "grid 1.5 -2.5 0.25",
        ], "test.race.txt");

        Assert.That(markers.LapCount, Is.EqualTo(2));
        Assert.That(markers.Start, Is.EqualTo(new RaceGate(new Vector2(1, 2), new Vector2(0, -1), 3, 4)));
        Assert.That(markers.Checkpoints, Has.Length.EqualTo(2));
        Assert.That(markers.Checkpoints[0].Point, Is.EqualTo(new Vector2(5, 6)));
        Assert.That(markers.Grid, Is.EqualTo(new[] { new GridSlot(new Vector2(1.5f, -2.5f), 0.25f) }));
    }

    [TestCase("bogus 1", "test.race.txt:3: unknown marker 'bogus'")]
    [TestCase("checkpoint 1 2 0 1 3", "test.race.txt:3: 'checkpoint' needs 6 numbers, got 5")]
    [TestCase("checkpoint 1 2 0 x 3 4", "test.race.txt:3: 'x' is not a number")]
    [TestCase("checkpoint 1 2 0 0 3 4", "test.race.txt:3: 'checkpoint' needs a forward direction, not zero")]
    [TestCase("checkpoint 1 2 0 1 -3 4", "test.race.txt:3: 'checkpoint' needs left and right of 0 or more")]
    [TestCase("start 1 2 0 1 3 4", "test.race.txt:3: 'start' is given twice")]
    [TestCase("laps 3", "test.race.txt:3: 'laps' is given twice")]
    public void Parse_BadLine_ThrowsWithTheLineNumber(string line, string message)
    {
        string[] lines = ["laps 3", "start 0 0 0 1 1 1", line];

        var exception = Assert.Throws<InvalidDataException>(() => RaceMarkers.Parse(lines, "test.race.txt"));
        Assert.That(exception.Message, Is.EqualTo(message));
    }

    [TestCase("laps 0", "test.race.txt:1: laps must be a whole number of at least 1")]
    [TestCase("laps 1.5", "test.race.txt:1: laps must be a whole number of at least 1")]
    public void Parse_BadLapCount_Throws(string line, string message)
    {
        var exception = Assert.Throws<InvalidDataException>(() => RaceMarkers.Parse([line], "test.race.txt"));
        Assert.That(exception.Message, Is.EqualTo(message));
    }

    [Test]
    public void Parse_NoStart_Throws()
    {
        var exception = Assert.Throws<InvalidDataException>(() => RaceMarkers.Parse(["laps 3"], "x.race.txt"));
        Assert.That(exception.Message, Is.EqualTo("x.race.txt: no 'start' line"));
    }

    [Test]
    public void Parse_NoLaps_Throws()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => RaceMarkers.Parse(["start 0 0 0 1 1 1"], "x.race.txt"));
        Assert.That(exception.Message, Is.EqualTo("x.race.txt: no 'laps' line"));
    }

    [Test]
    public void Load_ShippedStarCircuit_HasKartstersMarkers()
    {
        var markers = RaceMarkers.Load(Path.Combine("Assets", "Tracks"), "star-circuit");

        Assert.That(markers.LapCount, Is.EqualTo(3));
        Assert.That(markers.Checkpoints, Has.Length.EqualTo(7));
        Assert.That(markers.Grid, Has.Length.EqualTo(8));
        // The grid stands behind the line: Star Circuit's start faces -Z, so behind is +Z.
        Assert.That(markers.Start.Forward, Is.EqualTo(new Vector2(0, -1)));
        foreach (var slot in markers.Grid)
            Assert.That(slot.Position.Y, Is.GreaterThan(markers.Start.Point.Y));
    }

    private static IEnumerable<string> TrackIds()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Assets", "Tracks"), "*.race.txt"))
            yield return Path.GetFileName(file)[..^".race.txt".Length];
    }

    [Test]
    public void Cross_StoodGate_OnlyBetweenBottomAndTop()
    {
        var gate = new RaceGate(Vector2.Zero, new Vector2(0, -1), 5, 5) { Bottom = -2f, Top = 6f };

        Assert.That(gate.Cross(new Vector2(0, 1), new Vector2(0, -1), 0f), Is.EqualTo(1));
        Assert.That(gate.Cross(new Vector2(0, 1), new Vector2(0, -1), 7f), Is.Zero, "over it (a bridge)");
        Assert.That(gate.Cross(new Vector2(0, 1), new Vector2(0, -1), -3f), Is.Zero, "under it");
    }

    /// <summary>
    /// Every gate of a shipped track, stood on its road (<see cref="RaceGate.StandOn"/>): its own road's height is in
    /// it, and where another level of road runs over or under it (dunsfold-neon's bridge), that level is not.
    /// </summary>
    [TestCaseSource(nameof(TrackIds))]
    public void StandOn_ShippedTrack_TakesInItsRoadButNotAnotherLevel(string id)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Tracks");
        var track = Track.Load(folder, id);
        var markers = RaceMarkers.Load(folder, id);
        var gates = new List<RaceGate>(markers.Checkpoints) { markers.Start };
        var levels = 0;
        foreach (var marker in gates)
        {
            var gate = marker.StandOn(track);
            Assert.That(gate.Top - gate.Bottom, Is.GreaterThan(4f), $"{id}: room to drive through at {gate.Point}");
            for (var lateral = -gate.Left; lateral <= gate.Right; lateral += 0.5f)
            {
                var point = gate.Point + gate.Across * lateral;
                if (!track.TryGetGround(point.X, point.Y, float.MaxValue, out var top, out _, out var onRoad) || !onRoad)
                    continue;
                if (!track.TryGetGround(point.X, point.Y, top - 3f, out var under, out _, out var underRoad) || !underRoad)
                    continue;

                levels++;
                var inTop = top >= gate.Bottom && top <= gate.Top;
                var inUnder = under >= gate.Bottom && under <= gate.Top;
                Assert.That(inTop != inUnder, Is.True, $"{id}: roads at {top:0.0} and {under:0.0} m, gate " +
                    $"{gate.Bottom:0.0}..{gate.Top:0.0} m at {gate.Point}, {lateral:0.0} m across");
            }
        }

        if (id == "dunsfold-neon")
            Assert.That(levels, Is.GreaterThan(0), "a gate there lies by the bridge");
    }
}
