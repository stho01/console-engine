using System;
using System.IO;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Scenes.Menus;
using Kartster.Systems;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Tests.Race;

/// <summary>Pause, autopilot and the race's end as <c>TrackScene</c> wires them, headless on Star Circuit.</summary>
public class RaceEndingTests
{
    private const float Step = TrackScene.StepSeconds;
    private const int Karts = TrackScene.DefaultBotCount + 1;
    private const int Player = TrackScene.PlayerSlot;

    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static readonly Lazy<RaceMarkers> Markers =
        new(() => RaceMarkers.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static (SystemGroup Group, Entity[] Karts, RaceSession Race) Setup(World world, int laps,
        Func<Key, bool> isHeld = null)
    {
        var track = StarCircuit.Value;
        var tunings = new KartTuning[Karts];
        for (var i = 0; i < Karts; i++)
            tunings[i] = i % 2 == 0 ? KartCatalog.Olabil : KartCatalog.Wagon;
        var race = TrackScene.CreateSession(track, Markers.Value, laps);
        var karts = TrackScene.CreateKarts(world, track, Markers.Value, tunings, Player, race);
        var group = new SystemGroup(world);
        group.Add(new KartInputSystem(isHeld ?? (_ => false), race));
        TrackScene.AddDriving(group, track, tunings, race);
        group.Add(new AutopilotSystem());
        return (group, karts, race);
    }

    /// <summary>
    /// One frame as <c>EcsScene.RunSimulation</c> runs it, with <c>IsSimulationPaused</c> set while the pause menu is
    /// open, as <c>TrackScene</c> does.
    /// </summary>
    private static void Frame(SystemGroup group, PauseMenu pause, FixedTimestep timestep, float seconds)
    {
        if (pause.IsOpen)
            return;

        var steps = timestep.Advance(seconds);
        for (var i = 0; i < steps; i++)
            group.Update(timestep.StepSeconds);
    }

    [Test]
    public void Pause_WhileRacing_StopsTheClockAndKartsAndResumesWithoutATimeJump()
    {
        using var world = new World();
        var (group, karts, race) = Setup(world, laps: 3, key => key == Key.W);
        var timestep = new FixedTimestep(Step, TrackScene.MaxStepsPerFrame);
        var pause = new PauseMenu();
        const float frame = 1f / 60f;
        for (var i = 0; i < (RaceSession.CountdownSeconds + 2f) * 60; i++)
            Frame(group, pause, timestep, frame);
        Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));

        pause.Open();
        var clock = race.RaceTimeSeconds;
        var position = world.Get<Position>(karts[Player]).Value;
        var bot = world.Get<Position>(karts[1]).Value;
        var alpha = timestep.Alpha;
        for (var i = 0; i < 10 * 60; i++)
            Frame(group, pause, timestep, frame);

        Assert.That(race.RaceTimeSeconds, Is.EqualTo(clock), "the clock stops");
        Assert.That(world.Get<Position>(karts[Player]).Value, Is.EqualTo(position), "the player stops");
        Assert.That(world.Get<Position>(karts[1]).Value, Is.EqualTo(bot), "the bots stop");
        Assert.That(timestep.Alpha, Is.EqualTo(alpha), "the drawn karts don't move either");

        Assert.That(pause.Update(new MenuKeys(false, false, false, false, false, Back: true)),
            Is.EqualTo(PauseChoice.Resume));
        Frame(group, pause, timestep, frame);
        Assert.That(race.RaceTimeSeconds - clock, Is.EqualTo(frame).Within(Step + 1e-4f), "one frame later, no backlog");
        Assert.That(Vector2.Distance(world.Get<Position>(karts[Player]).Value, position), Is.GreaterThan(0f));
    }

    [Test]
    public void Pause_Keys_ResumeRestartOrQuit()
    {
        var down = new MenuKeys(false, true, false, false, false, false);
        var enter = new MenuKeys(false, false, false, false, true, false);
        var pause = new PauseMenu();
        Assert.That(pause.Update(enter), Is.EqualTo(PauseChoice.None), "closed: not listening");

        pause.Open();
        Assert.That(pause.Menu.Items[0].Label, Is.EqualTo(PauseMenu.Resume));
        Assert.That(pause.Update(enter), Is.EqualTo(PauseChoice.Resume));
        Assert.That(pause.IsOpen, Is.False);

        pause.Open();
        pause.Update(down);
        Assert.That(pause.Update(enter), Is.EqualTo(PauseChoice.Restart));

        pause.Open();
        pause.Update(down);
        pause.Update(down);
        Assert.That(pause.Update(enter), Is.EqualTo(PauseChoice.QuitToTitle));
        Assert.That(pause.IsOpen, Is.True, "the scene is about to go");

        pause.Open();
        Assert.That(pause.Menu.Selected, Is.Zero, "opens on RESUME");
    }

    [Test]
    public void Autopilot_PlayerFinished_BotDriverDrivesTheirKart()
    {
        using var world = new World();
        var track = StarCircuit.Value;
        var tunings = new[] { KartCatalog.Olabil };
        var karts = TrackScene.CreateKarts(world, track, Markers.Value, tunings, playerIndex: 0);
        var player = karts[0];
        world.Add(player, new RaceProgress { Finished = true });
        var group = new SystemGroup(world);
        group.Add(new KartInputSystem(_ => false));
        TrackScene.AddDriving(group, track, tunings);
        group.Add(new AutopilotSystem());
        var start = world.Get<Position>(player).Value;

        for (var step = 0; step < 10 * 120; step++)
            group.Update(Step);

        Assert.That(world.Has<Bot>(player), Is.True);
        Assert.That(world.Get<Bot>(player).Skill, Is.EqualTo(AutopilotSystem.Skill));
        Assert.That(world.Has<PlayerKart>(player), Is.True, "still the player's kart for the camera and the HUD");
        Assert.That(Vector2.Distance(world.Get<Position>(player).Value, start), Is.GreaterThan(80f),
            "drives on with no keys held");
        Assert.That(world.Get<Kart>(player).Surface, Is.EqualTo(Surface.Asphalt), "on the road");
    }

    [Test]
    public void Finish_PlayerHome_RemainingBotsFinishWithTimes()
    {
        using var world = new World();
        var (group, karts, race) = Setup(world, laps: 1);
        // A perfect player: the autopilot from the start, so they finish (and then drive on).
        world.Add(karts[Player], new Bot { Skill = AutopilotSystem.Skill, Sample = -1 });
        var finish = new RaceFinish(race, group, Step);

        for (var step = 0; step < 180 * 120 && !race.Get(Player).HasFinished; step++)
            group.Update(Step);
        Assert.That(race.Get(Player).HasFinished, Is.True, "the player gets round");
        Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing), "the race waits for everybody");

        Assert.That(finish.Step(30 * 60 * 120), Is.True);
        Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished));
        var names = new string[Karts];
        for (var i = 0; i < Karts; i++)
            names[i] = $"KART {i}";
        var results = new RaceReport(race, names, Player).Results("STAR-CIRCUIT");
        TestContext.Out.WriteLine(string.Join(", ", Array.ConvertAll(results.Rows,
            row => $"{row.Name} {(row.Finished ? row.TotalSeconds.ToString("0.0") : "DNF")}")));
        Assert.That(results.Rows, Has.All.Matches<Kartster.Ui.ResultRow>(row => row.Finished && row.TotalSeconds > 0f),
            "every bot gets home with a time");
        for (var i = 1; i < results.Rows.Length; i++)
            Assert.That(results.Rows[i].TotalSeconds, Is.GreaterThanOrEqualTo(results.Rows[i - 1].TotalSeconds));
    }

    [Test]
    public void CapSeconds_NobodyHome_IsHalfAnHour()
    {
        var race = TrackScene.CreateSession(StarCircuit.Value, Markers.Value, laps: 1);
        race.Register(0, isHuman: true);
        Assert.That(RaceFinish.CapSeconds(race), Is.EqualTo(RaceFinish.MaxSeconds));
    }
}
