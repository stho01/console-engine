using System;
using System.IO;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Systems;
using NUnit.Framework;

namespace Kartster.Tests.Race;

/// <summary>The race as <c>TrackScene</c> wires it, headless: World, the scene's systems, Star Circuit's markers.</summary>
public class RaceWiringTests
{
    private const float Step = TrackScene.StepSeconds;
    private const int Karts = TrackScene.DefaultBotCount + 1;

    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static readonly Lazy<RaceMarkers> Markers =
        new(() => RaceMarkers.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static KartTuning[] Tunings()
    {
        var tunings = new KartTuning[Karts];
        for (var i = 0; i < Karts; i++)
            tunings[i] = i % 2 == 0 ? KartCatalog.Olabil : KartCatalog.Wagon;
        return tunings;
    }

    /// <summary>The scene's set-up: karts on the grid, the input system, then the fixed-step driving and race.</summary>
    private static (SystemGroup Group, Entity[] Karts, RaceSession Race) Setup(World world, int laps, int playerIndex,
        Func<Key, bool> isHeld = null)
    {
        var track = StarCircuit.Value;
        var race = TrackScene.CreateSession(track, Markers.Value, laps);
        var tunings = Tunings();
        var karts = TrackScene.CreateKarts(world, track, Markers.Value, tunings, playerIndex, race);
        var group = new SystemGroup(world);
        group.Add(new KartInputSystem(isHeld ?? (_ => false), race));
        TrackScene.AddDriving(group, track, tunings, race);
        return (group, karts, race);
    }

    [Test]
    public void CreateKarts_WithMarkers_StandOnTheExportedGridWithThePlayerOnPole()
    {
        using var world = new World();
        var (_, karts, race) = Setup(world, laps: 3, TrackScene.PlayerSlot);

        Assert.That(race.Count, Is.EqualTo(Karts));
        for (var i = 0; i < karts.Length; i++)
        {
            var slot = Markers.Value.Grid[i];
            var position = world.Get<Position>(karts[i]).Value;
            Assert.That(position.X, Is.EqualTo(slot.Position.X).Within(1e-4f), $"kart {i}");
            Assert.That(position.Y, Is.EqualTo(slot.Position.Y).Within(1e-4f), $"kart {i}");
            Assert.That(world.Get<Rotation>(karts[i]).Radians, Is.EqualTo(KartFrame.ToRadians(slot.Heading)).Within(1e-5f));
            StarCircuit.Value.TryGetRoadHeight(new System.Numerics.Vector3(position.X, 0f, position.Y), out var road);
            Assert.That(world.Get<Kart>(karts[i]).Height, Is.EqualTo(road));
            Assert.That(world.Has<RaceProgress>(karts[i]), Is.True);
            Assert.That(race.Get(i).Position, Is.EqualTo(i + 1), "ranked as the grid before the start");
        }

        Assert.That(world.Has<PlayerKart>(karts[TrackScene.PlayerSlot]), Is.True);
        Assert.That(race.Get(TrackScene.PlayerSlot).IsHuman, Is.True);
        Assert.That(race.Get(1).IsHuman, Is.False);
    }

    [Test]
    public void StartingSlot_PastTheExportedGrid_GoesOnARowBehindTheKartTwoAhead()
    {
        var markers = Markers.Value;
        var extra = TrackScene.StartingSlot(StarCircuit.Value, markers, markers.Grid.Length);
        var twoAhead = markers.Grid[^2];

        Assert.That(extra.Heading, Is.EqualTo(twoAhead.Heading));
        Assert.That(System.Numerics.Vector2.Distance(extra.Position, twoAhead.Position),
            Is.EqualTo(TrackScene.GridRowSpacing).Within(1e-3f));
        Assert.That(extra.Position.Y, Is.GreaterThan(twoAhead.Position.Y), "heading 0 faces -Z, so behind is +Z");
    }

    [Test]
    public void Countdown_GasHeld_NobodyMovesUntilGo()
    {
        using var world = new World();
        var (group, karts, race) = Setup(world, laps: 3, TrackScene.PlayerSlot, key => key is Key.W or Key.D);
        var start = new Microsoft.Xna.Framework.Vector2[karts.Length];
        for (var i = 0; i < karts.Length; i++)
            start[i] = world.Get<Position>(karts[i]).Value;

        var countdownSteps = (int)(RaceSession.CountdownSeconds / Step) - 1;
        for (var step = 0; step < countdownSteps; step++)
            group.Update(Step);

        Assert.That(race.Phase, Is.EqualTo(RacePhase.Countdown));
        for (var i = 0; i < karts.Length; i++)
        {
            Assert.That(world.Get<Position>(karts[i]).Value, Is.EqualTo(start[i]), $"kart {i} waits");
            Assert.That(world.Get<KartControls>(karts[i]), Is.EqualTo(default(KartControls)), $"kart {i} controls");
        }

        for (var step = 0; step < 120; step++)
            group.Update(Step);

        Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));
        for (var i = 0; i < karts.Length; i++)
        {
            var moved = Microsoft.Xna.Framework.Vector2.Distance(world.Get<Position>(karts[i]).Value, start[i]);
            Assert.That(moved, Is.GreaterThan(1f), $"kart {i} drives after GO");
        }
    }

    [Test]
    public void Race_OneLapOfBots_EveryBotFinishesInOrder()
    {
        using var world = new World();
        var (group, karts, race) = Setup(world, laps: 1, playerIndex: -1);
        race.WaitsForEverybody = true;

        for (var step = 0; step < 180 * 120 && race.Phase != RacePhase.Finished; step++)
            group.Update(Step);

        Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished), "every bot gets round within 3 minutes");
        var winner = race.GetAtPosition(1);
        TestContext.Out.WriteLine($"winner kart {winner.Id}: {winner.FinishTimeSeconds:0.0} s");
        Assert.That(winner.HasFinished, Is.True);
        Assert.That(winner.BestLapSeconds, Is.EqualTo(winner.FinishTimeSeconds).Within(1e-3f));
        Assert.That(world.Get<RaceProgress>(karts[winner.Id]).FinishPosition, Is.EqualTo(1));
        for (var position = 2; position <= Karts; position++)
            Assert.That(race.GetAtPosition(position).FinishTimeSeconds, Is.GreaterThanOrEqualTo(winner.FinishTimeSeconds));

        var names = new string[Karts];
        for (var i = 0; i < Karts; i++)
            names[i] = $"BOT {i}";
        var results = new RaceReport(race, names, playerId: -1).Results("STAR-CIRCUIT");
        Assert.That(results.Rows, Has.Length.EqualTo(Karts));
        Assert.That(results.Rows[0].Name, Is.EqualTo($"BOT {winner.Id}"));
        Assert.That(results.Rows[0].TotalSeconds, Is.EqualTo(winner.FinishTimeSeconds));
        Assert.That(results.Rows[0].BestLapSeconds, Is.EqualTo(winner.BestLapSeconds));
        Assert.That(results.Rows, Has.All.Matches<Kartster.Ui.ResultRow>(row => row.Finished && !row.IsPlayer));
    }
}
