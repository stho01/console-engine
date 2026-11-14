using System;
using System.IO;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Modes;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Systems;
using Kartster.Tests.Ui;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Modes;

/// <summary>Time trial: the ghost's recording and playback, its file, the HUD, and a race of one on Star Circuit.
/// </summary>
public class TimeTrialTests
{
    private const float Step = TrackScene.StepSeconds;

    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static readonly Lazy<RaceMarkers> Markers =
        new(() => RaceMarkers.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private string _folder;

    [SetUp]
    public void CreateFolder() => Directory.CreateDirectory(_folder = Path.Combine(Path.GetTempPath(),
        "kartster-ghost-" + Guid.NewGuid().ToString("N")));

    [TearDown]
    public void DeleteFolder() => Directory.Delete(_folder, recursive: true);

    private static GhostPose Pose(int attempt, int step) =>
        new(step * 0.5f + attempt, -step * 0.25f, 0.1f * attempt, step * 0.01f);

    /// <summary>Drives <paramref name="steps"/> steps of lap 1 (poses from <see cref="Pose"/>), then ends the lap.</summary>
    private static void DriveLap(TimeTrial trial, int attempt, int steps, float lapSeconds)
    {
        var racer = new Racer { CurrentLap = 1 };
        for (var i = 0; i < steps; i++)
            trial.Step(racer, RacePhase.Racing, Pose(attempt, i));
        racer.CurrentLap = 2;
        racer.LastLapSeconds = lapSeconds;
        trial.Step(racer, RacePhase.Racing, Pose(attempt, 0));
    }

    [Test]
    public void Step_SecondAttempt_GhostReplaysTheBestLapExactly()
    {
        var trial = new TimeTrial(path: null);
        Assert.That(trial.TryGetGhost(1f, out _), Is.False, "no ghost before a lap is done");
        DriveLap(trial, attempt: 1, steps: 300, lapSeconds: 300 * Step);
        Assert.That(trial.Best.Count, Is.EqualTo(300));
        Assert.That(trial.Best.LapSeconds, Is.EqualTo(300 * Step));
        Assert.That(trial.LapTimes.ToArray(), Is.EqualTo(new[] { 300 * Step }));

        trial.Reset();
        Assert.That(trial.LapTimes.Length, Is.Zero);
        var racer = new Racer { CurrentLap = 1 };
        trial.Step(racer, RacePhase.Countdown, Pose(2, 0));
        Assert.That(trial.TryGetGhost(1f, out _), Is.False, "nothing during the countdown");
        for (var i = 0; i < 300; i++)
        {
            trial.Step(racer, RacePhase.Racing, Pose(2, i));
            Assert.That(trial.TryGetGhost(1f, out var ghost), Is.True);
            Assert.That(ghost, Is.EqualTo(Pose(1, i)), $"step {i}");
        }

        trial.Step(racer, RacePhase.Racing, Pose(2, 300));
        Assert.That(trial.TryGetGhost(1f, out _), Is.False, "gone past the best lap's end");
    }

    [Test]
    public void TryGetGhost_BetweenSteps_InterpolatesAsTheKarts()
    {
        var trial = new TimeTrial(path: null);
        DriveLap(trial, attempt: 1, steps: 10, lapSeconds: 1f);
        trial.Reset();
        var racer = new Racer { CurrentLap = 1 };
        trial.Step(racer, RacePhase.Racing, default);
        trial.Step(racer, RacePhase.Racing, default);

        trial.TryGetGhost(0f, out var behind);
        trial.TryGetGhost(0.5f, out var half);
        Assert.That(behind, Is.EqualTo(Pose(1, 0)), "alpha 0: the step before");
        Assert.That(half.X, Is.EqualTo((Pose(1, 0).X + Pose(1, 1).X) / 2).Within(1e-6f));
    }

    [Test]
    public void Step_SlowerLap_KeepsTheBest_FasterLapReplacesIt()
    {
        var trial = new TimeTrial(path: null);
        DriveLap(trial, attempt: 1, steps: 100, lapSeconds: 10f);
        trial.Reset();
        DriveLap(trial, attempt: 2, steps: 120, lapSeconds: 12f);
        Assert.That(trial.Best.LapSeconds, Is.EqualTo(10f));
        Assert.That(trial.Best[5], Is.EqualTo(Pose(1, 5)));

        trial.Reset();
        DriveLap(trial, attempt: 3, steps: 90, lapSeconds: 9f);
        Assert.That(trial.Best.LapSeconds, Is.EqualTo(9f));
        Assert.That(trial.Best.Count, Is.EqualTo(90));
        Assert.That(trial.Best[5], Is.EqualTo(Pose(3, 5)));
    }

    [Test]
    public void Step_LapLongerThanTheBuffer_IsNoGhost()
    {
        var trial = new TimeTrial(path: null, maxLapSteps: 50);
        DriveLap(trial, attempt: 1, steps: 60, lapSeconds: 1f);

        Assert.That(trial.Best.Count, Is.Zero);
        Assert.That(trial.LapTimes.Length, Is.EqualTo(1), "the time still counts");
    }

    [Test]
    public void Step_SteadyState_DoesNotAllocate()
    {
        var trial = new TimeTrial(path: null);
        DriveLap(trial, attempt: 1, steps: 2000, lapSeconds: 1f);
        trial.Reset();
        var racer = new Racer { CurrentLap = 1 };
        for (var i = 0; i < 500; i++) // warm up
        {
            trial.Step(racer, RacePhase.Racing, Pose(2, i));
            trial.TryGetGhost(0.5f, out _);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 500; i < 1500; i++)
        {
            trial.Step(racer, RacePhase.Racing, Pose(2, i));
            trial.TryGetGhost(0.5f, out _);
        }

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    [Test]
    public void GhostFile_SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(_folder, "star-circuit.olabil.ghost");
        var trial = new TimeTrial(path);
        DriveLap(trial, attempt: 1, steps: 250, lapSeconds: 41.5f);
        Assert.That(File.Exists(path), Is.True, "a new best lap is saved at once");
        Assert.That(trial.SaveFailed, Is.False);

        var again = new TimeTrial(path);
        Assert.That(again.Best.LapSeconds, Is.EqualTo(41.5f));
        Assert.That(again.Best.Poses.ToArray(), Is.EqualTo(trial.Best.Poses.ToArray()));
    }

    [Test]
    public void GhostFile_Damaged_IsIgnored()
    {
        var good = new GhostLap(10);
        good.Add(new GhostPose(1f, 2f, 0f, 0.5f));
        good.LapSeconds = 30f;
        var stream = new MemoryStream();
        GhostFile.Write(stream, good, Step);
        var bytes = stream.ToArray();

        Assert.That(Reads(bytes), Is.True, "the good file reads");
        Assert.That(Reads([]), Is.False, "empty");
        Assert.That(Reads(bytes[..^3]), Is.False, "cut short");
        Assert.That(Reads([.. bytes, 0]), Is.False, "trailing bytes");
        Assert.That(Reads(Patched(bytes, 0, 0x12345678)), Is.False, "not a ghost");
        Assert.That(Reads(Patched(bytes, 4, 99)), Is.False, "another version");
        Assert.That(Reads(Patched(bytes, 8, BitConverter.SingleToInt32Bits(1f / 60f))), Is.False, "another step");
        Assert.That(Reads(Patched(bytes, 12, BitConverter.SingleToInt32Bits(float.NaN))), Is.False, "NaN lap time");
        Assert.That(Reads(Patched(bytes, 16, 11)), Is.False, "more poses than the buffer");
        Assert.That(Reads(Patched(bytes, 20, BitConverter.SingleToInt32Bits(float.PositiveInfinity))), Is.False,
            "bad pose");

        var path = Path.Combine(_folder, "bad.ghost");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
        Assert.That(new TimeTrial(path).Best.Count, Is.Zero, "a damaged file: no ghost, no exception");
        return;

        static bool Reads(byte[] data)
        {
            var lap = new GhostLap(10);
            var read = GhostFile.Read(new MemoryStream(data), lap, Step);
            Assert.That(read || lap.Count == 0, Is.True, "nothing half-read is kept");
            return read;
        }

        static byte[] Patched(byte[] data, int offset, int value)
        {
            var copy = (byte[])data.Clone();
            BitConverter.TryWriteBytes(copy.AsSpan(offset), value);
            return copy;
        }
    }

    [Test]
    public void DrawUi_HidesStandingsAndShowsTheLapTimes_WithoutAllocating()
    {
        var trial = new TimeTrial(path: null);
        DriveLap(trial, attempt: 1, steps: 10, lapSeconds: 41.2f);
        var preview = new UiPreview();
        var hud = new HudState { StandingCount = 1, Standings = new HudStanding[1] };
        trial.DrawUi(preview.Target, ref hud);
        Assert.That(hud.StandingCount, Is.Zero);
        Assert.That(preview.Bounds(UiColors.Selected), Is.Not.Null, "the best lap of this attempt in yellow");
        Assert.That(preview.Bounds(UiColors.Sky).Value.Left, Is.LessThan(UiPreview.Width / 4), "bottom-left");

        var before = GC.GetAllocatedBytesForCurrentThread();
        trial.DrawUi(preview.Target, ref hud);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    [Test]
    public void TimeTrial_OnStarCircuit_HasNoBotsAndRecordsTheLapItTimes()
    {
        // As TrackScene wires a time trial: botCount 0, so one kart, the player's, and the recorder after the race.
        using var world = new World();
        var track = StarCircuit.Value;
        var tunings = new[] { KartCatalog.Olabil };
        var race = TrackScene.CreateSession(track, Markers.Value, laps: 2);
        var karts = TrackScene.CreateKarts(world, track, Markers.Value, tunings, TrackScene.PlayerSlot, race);
        Assert.That(new TrackScene(2, botCount: 0).BotCount, Is.Zero);
        Assert.That(race.Count, Is.EqualTo(1));
        Assert.That(world.Query(Query.With<Bot>()).Count, Is.Zero, "no bots");

        var trial = new TimeTrial(path: null);
        var group = new SystemGroup(world);
        group.Add(new KartInputSystem(_ => false, race));
        TrackScene.AddDriving(group, track, tunings, race);
        group.Add(new TimeTrialSystem(trial, race));
        // A perfect driver: the autopilot from the start.
        world.Add(karts[0], new Bot { Skill = AutopilotSystem.Skill, Sample = -1 });

        for (var step = 0; step < 180 * 120 && race.Get(0).CurrentLap < 2; step++)
            group.Update(Step);
        ref readonly var racer = ref race.Get(0);
        Assert.That(racer.CurrentLap, Is.EqualTo(2), "gets round");
        Assert.That(trial.Best.LapSeconds, Is.EqualTo(racer.LastLapSeconds));
        Assert.That(trial.Best.Count, Is.EqualTo((int)MathF.Round(racer.LastLapSeconds / Step)).Within(2),
            "one pose per step of the lap (the race clock is a float sum of steps)");
        Assert.That(trial.TryGetGhost(1f, out var ghost), Is.True, "lap 2 races the ghost of lap 1");
        var start = Markers.Value.Grid[0].Position;
        Assert.That(trial.Best[0].X, Is.EqualTo(start.X).Within(0.5f), "lap 1 starts on the grid");
        Assert.That(trial.Best[0].Z, Is.EqualTo(start.Y).Within(0.5f));
        Assert.That(float.IsFinite(ghost.X), Is.True);
    }
}
