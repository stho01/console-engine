using System;
using System.Numerics;
using Kartster.Race;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Race;

public class RaceReportTests
{
    private const int Player = 3;
    private static readonly string[] Names = ["EARL", "MERLIN", "STAG", "YOU"];

    // A straight: the line across x = 0 facing +x, a checkpoint at x = 100.
    private static RaceSession Session(int laps = 3)
    {
        var session = new RaceSession(new RaceGate(new Vector2(0, 0), new Vector2(1, 0), 5, 5),
            [new RaceGate(new Vector2(100, 0), new Vector2(1, 0), 5, 5)], laps);
        for (var id = 0; id < Names.Length; id++)
            session.Register(id, isHuman: id == Player);
        return session;
    }

    /// <summary>Drives kart <paramref name="id"/> along the straight in 1 m steps, 0.1 s each, then ranks.</summary>
    private static void Drive(RaceSession session, int id, float from, float to)
    {
        for (var x = from; x < to; x++)
        {
            session.Advance(0.1f);
            session.Update(id, new Vector2(x, 0), new Vector2(x + 1, 0));
        }

        session.UpdatePositions();
    }

    [Test]
    public void FillHud_Countdown_CountsThreeTwoOneThenGo()
    {
        var session = Session();
        var report = new RaceReport(session, Names, Player);
        var hud = new HudState();

        report.FillHud(ref hud, 0f);
        Assert.That(hud.Countdown, Is.EqualTo(3));
        session.Advance(1.5f);
        report.FillHud(ref hud, 0f);
        Assert.That(hud.Countdown, Is.EqualTo(2));
        Assert.That(hud.ShowGo, Is.False);

        session.Advance(1.5f);
        report.FillHud(ref hud, 0f);
        Assert.That(hud.Countdown, Is.Zero);
        Assert.That(hud.ShowGo, Is.True);

        session.Advance(RaceReport.GoSeconds);
        report.FillHud(ref hud, 0f);
        Assert.That(hud.ShowGo, Is.False);
        Assert.That(hud.Finished, Is.False);
        Assert.That(hud.PowerUp, Is.Null);
    }

    [Test]
    public void FillHud_PlayerFourth_ShowsTheTopThreeWithGapsAndThePlayersRow()
    {
        var session = Session();
        session.Advance(RaceSession.CountdownSeconds);
        var report = new RaceReport(session, Names, Player);
        Drive(session, 0, -5, 105); // the checkpoint at 10.5 s
        Drive(session, 1, -5, 105); // 11 s later
        Drive(session, 2, -5, 60);
        Drive(session, Player, -5, 40); // furthest from the checkpoint
        var hud = new HudState();

        report.FillHud(ref hud, 72.4f);

        Assert.That(hud.SpeedKmh, Is.EqualTo(72.4f));
        Assert.That(hud.Lap, Is.EqualTo(1));
        Assert.That(hud.LapCount, Is.EqualTo(3));
        Assert.That(hud.Position, Is.EqualTo(4));
        Assert.That(hud.KartCount, Is.EqualTo(4));
        Assert.That(hud.RaceSeconds, Is.EqualTo(session.RaceTimeSeconds));
        Assert.That(hud.StandingCount, Is.EqualTo(4));
        Assert.That(hud.Standings[0].Name, Is.EqualTo("EARL"));
        Assert.That(hud.Standings[0].Seconds, Is.EqualTo(10.5f).Within(1e-3f), "the leader's time at its last gate");
        Assert.That(hud.Standings[1].Name, Is.EqualTo("MERLIN"));
        Assert.That(hud.Standings[1].Seconds, Is.EqualTo(11f).Within(1e-3f), "behind the leader at the same gate");
        Assert.That(hud.Standings[2].Position, Is.EqualTo(3));
        Assert.That(hud.Standings[3], Is.EqualTo(new HudStanding
        {
            Position = 4, Name = "YOU", Seconds = session.GapToLeader(Player), IsPlayer = true
        }));
        Assert.That(hud.Standings[0].IsPlayer, Is.False);
    }

    [Test]
    public void FillHud_PlayerLeading_HasNoExtraRow()
    {
        var session = Session();
        session.Advance(RaceSession.CountdownSeconds);
        var report = new RaceReport(session, Names, Player);
        Drive(session, Player, -5, 20);
        var hud = new HudState();

        report.FillHud(ref hud, 0f);

        Assert.That(hud.Position, Is.EqualTo(1));
        Assert.That(hud.StandingCount, Is.EqualTo(RaceReport.TopStandings));
        Assert.That(hud.Standings[0].IsPlayer, Is.True);
    }

    [Test]
    public void Update_PlayerFinished_ResultsAfterTheDelayWithStragglersDnfAtTheCap()
    {
        var session = Session(laps: 1);
        session.Advance(RaceSession.CountdownSeconds);
        var report = new RaceReport(session, Names, Player);
        Drive(session, 1, -5, 40);
        Assert.That(report.Update(10f), Is.False, "still racing");

        // Line, checkpoint, line: one lap on a straight that is driven back to the start in one jump (too long to count).
        Drive(session, Player, -5, 105);
        session.Update(Player, new Vector2(105, 0), new Vector2(-1, 0));
        Drive(session, Player, -1, 1);
        Assert.That(session.Get(Player).HasFinished, Is.True);

        Assert.That(report.Update(RaceReport.ResultsDelaySeconds - 0.1f), Is.False, "FINISHED shows first");
        Assert.That(report.Update(0.2f), Is.True);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Racing), "the rest are finished by a RaceFinish");

        // Nobody else gets home: the clock runs to the cap (twice the winner's time plus 30 s), then the race stops.
        using var world = new ConsoleEngine.Ecs.World();
        var clock = new ConsoleEngine.Ecs.SystemGroup(world);
        clock.Add(new ClockSystem(session));
        var finish = new RaceFinish(session, clock, 0.1f);
        Assert.That(finish.Step(100_000), Is.True);
        Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
        Assert.That(session.RaceTimeSeconds, Is.EqualTo(RaceFinish.CapSeconds(session)).Within(0.11f));
        Assert.That(RaceFinish.CapSeconds(session),
            Is.EqualTo(session.Get(Player).FinishTimeSeconds * RaceFinish.WinnerFactor + RaceFinish.SlackSeconds));

        var results = report.Results("STAR-CIRCUIT");
        Assert.That(results.TrackName, Is.EqualTo("STAR-CIRCUIT"));
        Assert.That(results.Rows, Has.Length.EqualTo(Names.Length));
        Assert.That(results.Rows[0], Is.EqualTo(new ResultRow
        {
            Position = 1, Name = "YOU", TotalSeconds = session.Get(Player).FinishTimeSeconds,
            BestLapSeconds = session.Get(Player).BestLapSeconds, Finished = true, IsPlayer = true
        }));
        Assert.That(results.Rows[1].Name, Is.EqualTo("MERLIN"), "then by gates passed");
        for (var i = 1; i < results.Rows.Length; i++)
        {
            Assert.That(results.Rows[i].Position, Is.EqualTo(i + 1));
            Assert.That(results.Rows[i].Finished, Is.False, "DNF");
            Assert.That(results.Rows[i].IsPlayer, Is.False);
        }
    }

    [Test]
    public void FillHud_EveryFrame_DoesNotAllocate()
    {
        var session = Session();
        session.Advance(RaceSession.CountdownSeconds);
        var report = new RaceReport(session, Names, Player);
        Drive(session, 0, -5, 20);
        var hud = new HudState();
        report.FillHud(ref hud, 50f);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 1000; frame++)
            report.FillHud(ref hud, frame);

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    private sealed class ClockSystem(RaceSession session) : ConsoleEngine.Ecs.EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) => session.Advance(deltaSeconds);
    }
}
