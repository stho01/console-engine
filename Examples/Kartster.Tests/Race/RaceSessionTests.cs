using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Kartster.Race;
using NUnit.Framework;

namespace Kartster.Tests.Race;

/// <summary>
/// A synthetic square loop, 100 m a side round the origin, driven anticlockwise seen from above (x then y): route
/// distance 0 is the corner (-50, -50), the start line is at 50 m, the checkpoints at 150, 250 and 350 m.
/// </summary>
public class RaceSessionTests
{
    private const float Step = 1f / 120f;
    private const float Lap = 400f;
    private const float Grid = 40f; // 10 m behind the line

    private static readonly RaceGate Start = new(new Vector2(0, -50), new Vector2(1, 0), 5, 5);
    private static readonly RaceGate[] Checkpoints =
    [
        new(new Vector2(50, 0), new Vector2(0, 1), 5, 5),
        new(new Vector2(0, 50), new Vector2(-1, 0), 5, 5),
        new(new Vector2(-50, 0), new Vector2(0, -1), 5, 5),
    ];

    private static Vector2 At(float distance)
    {
        var s = distance - MathF.Floor(distance / Lap) * Lap;
        return s switch
        {
            < 100 => new Vector2(-50 + s, -50),
            < 200 => new Vector2(50, -50 + (s - 100)),
            < 300 => new Vector2(50 - (s - 200), 50),
            _ => new Vector2(-50, 50 - (s - 300)),
        };
    }

    private static RaceSession Session(int laps = 3, params int[] ids)
    {
        var session = new RaceSession(Start, Checkpoints, laps);
        foreach (var id in ids.Length == 0 ? [1] : ids)
            session.Register(id);
        return session;
    }

    private static RaceSession Racing(int laps = 3, params int[] ids)
    {
        var session = Session(laps, ids);
        session.Advance(RaceSession.CountdownSeconds);
        return session;
    }

    /// <summary>Drives a kart from route distance <paramref name="from"/> to <paramref name="to"/> in 1 m steps.</summary>
    private static void Drive(RaceSession session, int id, float from, float to, float secondsPerMeter = 0f)
    {
        var direction = MathF.Sign(to - from);
        for (var s = from; direction > 0 ? s < to : s > to; s += direction)
        {
            session.Advance(secondsPerMeter);
            session.Update(id, At(s), At(s + direction));
        }
    }

    [Test]
    public void Countdown_UntilThreeSeconds_BlocksDriving()
    {
        var session = Session();

        session.Advance(2.9f);
        Assert.That(session.CanDrive, Is.False);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Countdown));

        session.Advance(0.15f);
        Assert.That(session.CanDrive, Is.True);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Racing));
        Assert.That(session.RaceTimeSeconds, Is.Zero, "the overshoot is not carried into the clock");
    }

    [Test]
    public void GapToLeader_SameGate_IsTheTimeBehindWhoGotThereFirst()
    {
        var session = Racing(3, 1, 2);
        Assert.That(session.GapToLeader(2), Is.Zero, "no gate yet");

        Drive(session, 1, Grid, 160, secondsPerMeter: 0.1f); // the line at 1 s, the first checkpoint at 11 s
        Drive(session, 2, Grid, 160, secondsPerMeter: 0.1f); // the same, 12 s later

        Assert.That(session.Get(1).GateSeconds, Is.EqualTo(11f).Within(1e-3f));
        Assert.That(session.GapToLeader(1), Is.Zero.Within(1e-4f));
        Assert.That(session.GapToLeader(2), Is.EqualTo(12f).Within(1e-3f));
    }

    [Test]
    public void Update_DuringCountdown_PassesGatesButCountsNoLap()
    {
        var session = Session();

        Drive(session, 1, Grid, Grid + Lap + 20);

        Assert.That(session.Get(1).CompletedLaps, Is.EqualTo(1));
        Assert.That(session.Get(1).CurrentLap, Is.EqualTo(1));
        Assert.That(session.Get(1).LastLapSeconds, Is.Zero);
    }

    [Test]
    public void Update_FirstCrossingOfTheLine_StartsTheLapWithoutCountingOne()
    {
        var session = Racing();

        Drive(session, 1, Grid, Grid + 20);

        ref readonly var racer = ref session.Get(1);
        Assert.That(racer.LapStarted, Is.True);
        Assert.That(racer.CompletedLaps, Is.Zero);
        Assert.That(racer.CurrentLap, Is.EqualTo(1));
    }

    [Test]
    public void Update_OnceRound_CountsALapAndTimesIt()
    {
        var session = Racing();

        Drive(session, 1, Grid, Grid + Lap + 20, secondsPerMeter: 0.05f);

        ref readonly var racer = ref session.Get(1);
        Assert.That(racer.CompletedLaps, Is.EqualTo(1));
        Assert.That(racer.CurrentLap, Is.EqualTo(2));
        Assert.That(racer.MissedCheckpoints, Is.Zero);
        // From the green light (on the grid, 10 m before the line) to the line a lap later: 410 m at 0.05 s/m.
        Assert.That(racer.LastLapSeconds, Is.EqualTo(410 * 0.05f).Within(0.06f));
        Assert.That(racer.BestLapSeconds, Is.EqualTo(racer.LastLapSeconds));
    }

    [Test]
    public void Update_FasterSecondLap_IsTheBestLap()
    {
        var session = Racing();

        Drive(session, 1, Grid, Grid + Lap + 20, secondsPerMeter: 0.05f);
        Drive(session, 1, Grid + Lap + 20, Grid + 2 * Lap + 20, secondsPerMeter: 0.04f);

        ref readonly var racer = ref session.Get(1);
        Assert.That(racer.LastLapSeconds, Is.EqualTo(10 * 0.05f + (Lap - 10) * 0.04f).Within(0.06f));
        Assert.That(racer.BestLapSeconds, Is.EqualTo(racer.LastLapSeconds));
    }

    [Test]
    public void Update_ForwardThroughTheNextCheckpoint_CountsIt()
    {
        var session = Racing();

        Drive(session, 1, Grid, 160);

        Assert.That(session.Get(1).NextCheckpointIndex, Is.EqualTo(1));
    }

    [Test]
    public void Update_BackThroughTheLastCheckpoint_TakesItBack()
    {
        var session = Racing();
        Drive(session, 1, Grid, 160);

        Drive(session, 1, 160, 140);
        Assert.That(session.Get(1).NextCheckpointIndex, Is.Zero);

        Drive(session, 1, 140, 160);
        Assert.That(session.Get(1).NextCheckpointIndex, Is.EqualTo(1));
    }

    [Test]
    public void Update_BackOverTheLineBeforeTheLap_UnstartsIt()
    {
        var session = Racing();
        Drive(session, 1, Grid, 60);

        Drive(session, 1, 60, 40);

        Assert.That(session.Get(1).LapStarted, Is.False);
    }

    [Test]
    public void Update_BackOverTheLineAfterALap_TakesTheLapBackAndCountsItOnce()
    {
        var session = Racing();
        Drive(session, 1, Grid, Grid + Lap + 20, secondsPerMeter: 0.05f);
        var lapSeconds = session.Get(1).LastLapSeconds;

        Drive(session, 1, Grid + Lap + 20, Grid + Lap, secondsPerMeter: 0.05f);
        Assert.That(session.Get(1).CompletedLaps, Is.Zero);
        Assert.That(session.Get(1).NextCheckpointIndex, Is.EqualTo(Checkpoints.Length));

        Drive(session, 1, Grid + Lap, Grid + Lap + 20, secondsPerMeter: 0.05f);
        ref readonly var racer = ref session.Get(1);
        Assert.That(racer.CompletedLaps, Is.EqualTo(1));
        Assert.That(racer.CurrentLap, Is.EqualTo(2));
        Assert.That(racer.LastLapSeconds, Is.EqualTo(lapSeconds), "the same lap is not counted twice");
    }

    [Test]
    public void Update_BesideTheGate_CrossesNothing()
    {
        var session = Racing();

        session.Update(1, new Vector2(-1, -60), new Vector2(1, -60)); // 10 m right of the line, which reaches 5

        Assert.That(session.Get(1).LapStarted, Is.False);
    }

    [Test]
    public void Update_StepLongerThanTenMetres_CrossesNothing()
    {
        var session = Racing();

        session.Update(1, new Vector2(-5.5f, -50), new Vector2(5.5f, -50));
        Assert.That(session.Get(1).LapStarted, Is.False);

        session.Update(1, new Vector2(-4.5f, -50), new Vector2(4.5f, -50));
        Assert.That(session.Get(1).LapStarted, Is.True);
    }

    [Test]
    public void Update_ThroughTheGateAfterNext_CountsAMissedCheckpoint()
    {
        var session = Racing();
        Drive(session, 1, Grid, 60);

        // Through the second checkpoint with the first still owed, as after a cut across the square.
        session.Update(1, new Vector2(1, 49), new Vector2(-1, 49));

        ref readonly var racer = ref session.Get(1);
        Assert.That(racer.MissedCheckpoints, Is.EqualTo(1));
        Assert.That(racer.NextCheckpointIndex, Is.Zero, "the checkpoint is still owed");
    }

    [Test]
    public void Update_LastLap_FinishesARaceOfOne()
    {
        var session = Racing(laps: 2);

        Drive(session, 1, Grid, Grid + 2 * Lap + 20, secondsPerMeter: 0.01f);

        ref readonly var racer = ref session.Get(1);
        Assert.That(racer.HasFinished, Is.True);
        Assert.That(racer.FinishPosition, Is.EqualTo(1));
        Assert.That(racer.CurrentLap, Is.EqualTo(2));
        Assert.That(racer.FinishTimeSeconds, Is.EqualTo((2 * Lap + 10) * 0.01f).Within(0.05f));
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
    }

    [Test]
    public void Finish_BotsOnly_EndsWhenOneIsLeft()
    {
        var session = Racing(1, 1, 2, 3);

        Drive(session, 1, Grid, Grid + Lap + 20);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Racing));

        Drive(session, 2, Grid, Grid + Lap + 20);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
        Assert.That(session.Get(3).HasFinished, Is.False, "the last one stays unfinished");
    }

    [Test]
    public void Finish_WithAPerson_WaitsForEverybody()
    {
        var session = new RaceSession(Start, Checkpoints, 1);
        session.Register(1, isHuman: true);
        session.Register(2);
        session.Advance(RaceSession.CountdownSeconds);

        Drive(session, 2, Grid, Grid + Lap + 20);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Racing));

        Drive(session, 1, Grid, Grid + Lap + 20);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
        Assert.That(session.Get(1).FinishPosition, Is.EqualTo(2));
    }

    [Test]
    public void Stop_WhileRacing_LeavesTheRestUnfinished()
    {
        var session = Racing(1, 1, 2);

        session.Stop();

        Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
        Assert.That(session.Get(1).HasFinished, Is.False);
    }

    [Test]
    public void UpdatePositions_ByLapsThenGatesThenDistance()
    {
        var session = Racing(3, 1, 2, 3, 4);
        Drive(session, 1, Grid, 120);              // lap started, next checkpoint 0, 30 m to it
        Drive(session, 2, Grid, 160);              // one checkpoint more
        Drive(session, 3, Grid, 130);              // level with 1 on gates, 20 m to the checkpoint
        Drive(session, 4, Grid, Grid + Lap + 20);  // a lap ahead

        session.UpdatePositions();

        Assert.That(Positions(session, 1, 2, 3, 4), Is.EqualTo(new[] { 4, 2, 3, 1 }));
        Assert.That(session.GetAtPosition(1).Id, Is.EqualTo(4));
    }

    [Test]
    public void UpdatePositions_OnTheGrid_RanksByDistanceToTheLine()
    {
        var session = Session(3, 1, 2);
        session.Update(1, At(30), At(30));
        session.Update(2, At(Grid), At(Grid));

        session.UpdatePositions();

        Assert.That(Positions(session, 1, 2), Is.EqualTo(new[] { 2, 1 }));
    }

    [Test]
    public void UpdatePositions_Level_KeepsRegistrationOrder()
    {
        var session = Racing(3, 7, 3, 5);
        foreach (var id in new[] { 7, 3, 5 })
            Drive(session, id, Grid, 100);

        session.UpdatePositions();

        Assert.That(Positions(session, 7, 3, 5), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void UpdatePositions_Finished_AreAheadInFinishingOrder()
    {
        var session = Racing(1, 1, 2, 3, 4);
        Drive(session, 2, Grid, Grid + Lap + 20);         // finishes first...
        Drive(session, 2, Grid + Lap + 20, Grid + Lap);   // ...then backs over the line, which gives a lap back
        Drive(session, 1, Grid, Grid + Lap + 20);         // finishes second, further round than 2
        Drive(session, 3, Grid, Grid + Lap - 10);         // nearly home
        Drive(session, 4, Grid, 300);

        session.UpdatePositions();

        Assert.That(session.Get(2).HasFinished, Is.True, "a result is not taken back");
        Assert.That(Positions(session, 2, 1, 3, 4), Is.EqualTo(new[] { 1, 2, 3, 4 }));
    }

    [Test]
    public void Register_Twice_Throws()
    {
        var session = Session();

        Assert.That(() => session.Register(1), Throws.ArgumentException.With.Message.StartWith(
            "Kart 1 is already registered with the race"));
    }

    [Test]
    public void Update_UnknownKart_Throws()
    {
        var session = Session();

        Assert.That(() => session.Update(9, Vector2.Zero, Vector2.Zero),
            Throws.TypeOf<KeyNotFoundException>().With.Message.EqualTo("Kart 9 is not registered with the race"));
    }

    [Test]
    public void Update_InSteadyState_DoesNotAllocate()
    {
        var session = Racing(100, 1, 2, 3, 4);
        var distance = Grid;
        for (var step = 0; step < 100; step++, distance += 0.25f)
            OneStep(session, distance);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 4000; step++, distance += 0.25f)
            OneStep(session, distance);

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        Assert.That(session.Get(1).CompletedLaps, Is.GreaterThan(1));

        static void OneStep(RaceSession session, float distance)
        {
            session.Advance(Step);
            for (var id = 1; id <= 4; id++)
                session.Update(id, At(distance - id - 0.25f), At(distance - id));
            session.UpdatePositions();
        }
    }

    [Test]
    public void StarCircuit_DrivenAlongTheCenterline_FinishesAfterThreeLaps()
    {
        var folder = Path.Combine("Assets", "Tracks");
        var track = Track.Load(folder, "star-circuit");
        var markers = RaceMarkers.Load(folder, "star-circuit");
        var session = new RaceSession(markers);
        session.Register(1, isHuman: true);
        session.Advance(RaceSession.CountdownSeconds);

        const float StepMeters = 0.25f; // 30 m/s at 120 steps a second
        float? lapStartedAt = null;
        float? finishedAt = null;
        var gatesPassed = 0;
        var lastGate = (session.Get(1).CompletedLaps, session.Get(1).NextCheckpointIndex);
        var previous = Flat(track.Sample(0).Position);
        for (var distance = StepMeters; distance < 5 * track.Length && finishedAt is null; distance += StepMeters)
        {
            var position = Flat(track.Sample(distance).Position);
            session.Advance(StepMeters / 30f);
            session.Update(1, previous, position);
            previous = position;

            ref readonly var racer = ref session.Get(1);
            if (racer.LapStarted && lapStartedAt is null)
                lapStartedAt = distance;
            if ((racer.CompletedLaps, racer.NextCheckpointIndex) != lastGate)
                gatesPassed++;
            lastGate = (racer.CompletedLaps, racer.NextCheckpointIndex);
            if (racer.HasFinished)
                finishedAt = distance;
        }

        ref readonly var result = ref session.Get(1);
        Assert.That(lapStartedAt, Is.Not.Null);
        Assert.That(finishedAt, Is.EqualTo(lapStartedAt.Value + 3 * track.Length).Within(1f));
        Assert.That(result.MissedCheckpoints, Is.Zero);
        Assert.That(gatesPassed, Is.EqualTo(3 * (markers.Checkpoints.Length + 1)), "every checkpoint, every lap");
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
        Assert.That(result.FinishTimeSeconds, Is.EqualTo(finishedAt.Value / 30f).Within(0.1f));

        static Vector2 Flat(Vector3 position) => new(position.X, position.Z);
    }

    private static int[] Positions(RaceSession session, params int[] ids)
    {
        var positions = new int[ids.Length];
        for (var i = 0; i < ids.Length; i++)
            positions[i] = session.Get(ids[i]).Position;
        return positions;
    }
}
