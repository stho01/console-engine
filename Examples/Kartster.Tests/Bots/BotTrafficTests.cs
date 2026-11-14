using System;
using System.IO;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Bots;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Systems;
using NUnit.Framework;
using Vector2 = System.Numerics.Vector2;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Tests.Bots;

/// <summary>Bots among other karts: passing, following, leaning, staying on the road, a whole race.</summary>
public class BotTrafficTests
{
    private const float Step = TrackScene.StepSeconds;

    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static readonly Lazy<BotRoad> StarRoad = new(() => new BotRoad(StarCircuit.Value));

    [Test]
    public void BotRoad_StarCircuit_WidthsReachTheEdgeOfTheAsphalt()
    {
        var track = StarCircuit.Value;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var road = new BotRoad(track);
        var milliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var narrowest = float.MaxValue;
        var widest = 0f;
        for (var i = 0; i < road.Count; i++)
        {
            narrowest = MathF.Min(narrowest, MathF.Min(road.LeftWidth(i), road.RightWidth(i)));
            widest = MathF.Max(widest, MathF.Max(road.LeftWidth(i), road.RightWidth(i)));
            var right = Right(road.ForwardAt(i));
            Assert.That(IsOnRoad(track, road.PointAt(i) + right * (road.RightWidth(i) - 0.15f)), Is.True);
            Assert.That(IsOnRoad(track, road.PointAt(i) - right * (road.LeftWidth(i) - 0.15f)), Is.True);
            Assert.That(IsOnRoad(track, road.PointAt(i) + right * (road.RightWidth(i) + 0.2f)), Is.False);
        }

        TestContext.Out.WriteLine($"{road.Count} samples, {road.Length:0} m, half-widths {narrowest:0.0} to " +
                                  $"{widest:0.0} m, measured in {milliseconds:0} ms");
        Assert.That(narrowest, Is.GreaterThan(BotTraffic.EdgeMargin + BotTraffic.LaneWidth / 2f));
    }

    [Test]
    public void Avoid_SlowerKartAheadOnTheLane_MovesALaneToItsOwnSide()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts = [Kart(100f, 0.3f, 20f), Kart(110f, 0f, 10f)];
        var limit = float.MaxValue;

        var offset = BotTraffic.Avoid(road, karts, 0, -1, 0f, 20f, -9f, 9f, ref limit);

        Assert.That(offset, Is.EqualTo(BotTraffic.LaneWidth).Within(1e-4f));
    }

    [Test]
    public void Avoid_OwnSideOffTheRoad_PassesOnTheOtherSide()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts = [Kart(100f, 0.3f, 20f), Kart(110f, 0f, 10f)];
        var limit = float.MaxValue;

        var offset = BotTraffic.Avoid(road, karts, 0, -1, 0f, 20f, -9f, 1f, ref limit);

        Assert.That(offset, Is.EqualTo(-BotTraffic.LaneWidth).Within(1e-4f));
    }

    [Test]
    public void Avoid_KartAlongside_KeepsALaneAway()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts = [Kart(100f, -1f, 20f), Kart(101f, 1f, 20f)];
        var limit = float.MaxValue;

        var offset = BotTraffic.Avoid(road, karts, 0, -1, -1f, 20f, -9f, 9f, ref limit);

        Assert.That(offset, Is.EqualTo(1f - BotTraffic.LaneWidth).Within(1e-4f));
        Assert.That(limit, Is.EqualTo(float.MaxValue), "not in its path");
    }

    [Test]
    public void Avoid_LaneWouldLeaveTheRoad_StaysInside()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts = [Kart(100f, 2f, 20f), Kart(101f, 1.5f, 20f)];
        var limit = float.MaxValue;

        var offset = BotTraffic.Avoid(road, karts, 0, -1, 2f, 20f, -3f, 3f, ref limit);

        Assert.That(offset, Is.EqualTo(3f), "a lane right of the other kart would be 3.7");
    }

    [Test]
    public void Avoid_KartAheadAndBothSidesTaken_FollowsAtItsSpeed()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts =
        [
            Kart(100f, 0f, 20f),
            Kart(106f, 0f, 8f),
            Kart(107f, -BotTraffic.LaneWidth, 8f),
            Kart(107f, BotTraffic.LaneWidth, 8f),
        ];
        var limit = float.MaxValue;

        var offset = BotTraffic.Avoid(road, karts, 0, -1, 0f, 20f, -9f, 9f, ref limit);

        Assert.That(offset, Is.Zero);
        Assert.That(limit, Is.EqualTo(8f + (6f - BotTraffic.FollowMeters) / BotTraffic.FollowSeconds).Within(1e-3f));
    }

    [Test]
    public void Lean_KartBesideAndAggressive_AimsOntoItThenLetsGo()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts = [Kart(100f, 0f, 20f), Kart(101f, 2f, 20f)];
        var state = BotTrafficState.Start(0, 0f);

        var first = BotTraffic.Lean(road, karts, 0, ref state, 1f, false, 0f, -9f, 9f, Step);
        var leaning = BotTraffic.Lean(road, karts, 0, ref state, 1f, false, 0f, -9f, 9f, Step);
        var braking = BotTraffic.Lean(road, karts, 0, ref state, 1f, true, 0f, -9f, 9f, Step);

        Assert.That(first, Is.Zero, "decides this step");
        Assert.That(leaning, Is.EqualTo(2f - BotTraffic.LeanMeters).Within(1e-4f));
        Assert.That(braking, Is.Zero, "never while braking for a corner");
        Assert.That(state.LeanCooldownSeconds, Is.EqualTo(BotTraffic.LeanCooldownSeconds));
    }

    [Test]
    public void Lean_NoAggression_NeverLeans()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts = [Kart(100f, 0f, 20f), Kart(101f, 2f, 20f)];
        var state = BotTrafficState.Start(0, 0f);

        for (var i = 0; i < 10 * 120; i++)
            Assert.That(BotTraffic.Lean(road, karts, 0, ref state, 0f, false, 0f, -9f, 9f, Step), Is.Zero);
    }

    [Test]
    public void BumpedBy_FreeBot_GoesThroughTheBumpersLineThenLetsGo()
    {
        var road = StarRoad.Value;
        TrafficKart[] karts = [Kart(100f, 0f, 20f), Kart(99f, 1.5f, 22f)];
        var state = BotTrafficState.Start(0, 0f);

        BotTraffic.BumpedBy(ref state, 1);
        var aim = BotTraffic.Lean(road, karts, 0, ref state, 0f, false, 0f, -9f, 9f, Step);
        var steps = 1;
        while (state.LeanTarget >= 0 && steps < 10 * 120)
        {
            BotTraffic.Lean(road, karts, 0, ref state, 0f, false, 0f, -9f, 9f, Step);
            steps++;
        }

        Assert.That(aim, Is.EqualTo(1.5f + BotTraffic.BumpThroughMeters).Within(1e-4f));
        Assert.That(steps * Step, Is.EqualTo(BotTraffic.BumpBackSeconds).Within(Step * 1.5f));
        Assert.That(state.IsBumpingBack, Is.False);
        Assert.That(state.LastLeanTarget, Is.EqualTo(1));
    }

    [Test]
    public void BumpedBy_SameKartDuringTheCooldownOrBusy_IgnoresIt()
    {
        var state = BotTrafficState.Start(0, 0f);
        BotTraffic.BumpedBy(ref state, 1);
        BotTraffic.Bumped(ref state, 1);
        Assert.That(state.LeanTarget, Is.EqualTo(-1), "its bump back landed: done");

        BotTraffic.BumpedBy(ref state, 1);
        Assert.That(state.LeanTarget, Is.EqualTo(-1), "that kart is left alone for the cooldown");

        BotTraffic.BumpedBy(ref state, 2);
        BotTraffic.BumpedBy(ref state, 3);
        Assert.That(state.LeanTarget, Is.EqualTo(2), "another kart: goes for it, one at a time");
    }

    [Test]
    public void AWasBumper_ByClosingSpeedAlongTheLine_FasterOneBumpsAndSlowMeetingsDont()
    {
        var a = Vector2.Zero;
        var b = new Vector2(2f, 0f);

        Assert.That(BotTraffic.AWasBumper(a, new Vector2(10f, 0f), b, new Vector2(4f, 0f), out var bump), Is.True);
        Assert.That(bump, Is.True);
        Assert.That(BotTraffic.AWasBumper(a, new Vector2(4f, 0f), b, new Vector2(-6f, 0f), out bump), Is.False);
        Assert.That(bump, Is.True);
        BotTraffic.AWasBumper(a, new Vector2(5f, 0f), b, new Vector2(4f, 0f), out bump);
        Assert.That(bump, Is.False, "1 m/s closing is a touch, not a bump");
    }

    [TestCase(1f, true)]
    [TestCase(0.3f, false)]
    public void Bump_KartDrivesIntoABotsRearCorner_BotBumpsBackOnlyIfSkilled(float skill, bool bumpsBack)
    {
        Assert.That(BotDriverSystem.BumpsBack(skill), Is.EqualTo(bumpsBack));
        var track = StarCircuit.Value;
        var road = StarRoad.Value;
        using var world = new World();
        KartTuning[] tunings = [KartCatalog.Kart, KartCatalog.Kart];
        var start = LongestStraight(road, new BotRoutePlan(road, tunings[0], 0.8f, 0.7f), out _);
        var forward = road.ForwardAt(start);
        var right = Right(forward);
        var bot = BotAt(world, road, tunings[0], start, 0);
        world.Get<Bot>(bot).Skill = skill;
        world.Get<Velocity>(bot).Value = new XnaVector2(forward.X, forward.Y) * 10f;

        // 1.5 m behind (too far back to be leaned on) and 1.6 m to the right, 20 degrees into the bot, a bit faster.
        var from = road.PointAt(start) - forward * 1.5f + right * 1.6f;
        var into = float.DegreesToRadians(20f);
        var heading = forward * MathF.Cos(into) - right * MathF.Sin(into);
        var bumper = KartEntity.Create(world, tunings[1], new XnaVector2(from.X, from.Y),
            KartFrame.HeadingOf(new System.Numerics.Vector3(heading.X, 0f, heading.Y)), 1);
        world.Get<Velocity>(bumper).Value = new XnaVector2(heading.X, heading.Y) * 10.5f;
        var (group, bots) = Driving(world, track, tunings);

        var wentForIt = false;
        for (var step = 0; step < 60; step++)
        {
            group.Update(Step);
            wentForIt |= bots.IsBumpingBack(0);
        }

        Assert.That(wentForIt, Is.EqualTo(bumpsBack));
    }

    [Test]
    public void RubberBand_BotFarAheadOfTheHuman_EasesOffByATenth()
    {
        TrafficKart[] karts = [Kart(0f, 0f, 0f) with { Driven = 500f }, Kart(0f, 0f, 0f) with { IsHuman = true }];

        Assert.That(BotTraffic.RubberBand(karts, 0), Is.EqualTo(0.9f).Within(1e-5f));
        Assert.That(BotTraffic.RubberBand([karts[0]], 0), Is.EqualTo(1f), "no human");
    }

    [Test]
    public void Overtake_FasterBotBehindASlowerOneOnAStraight_ChangesLaneAndPasses()
    {
        var track = StarCircuit.Value;
        var road = StarRoad.Value;
        using var world = new World();
        KartTuning[] tunings = [KartCatalog.Kart with { EnginePowerWatts = 8000f }, KartCatalog.Kart];
        var start = LongestStraight(road, new BotRoutePlan(road, tunings[1], 0.8f, 0.7f), out var straight);
        var slow = BotAt(world, road, tunings[0], start + 8, 0);
        var fast = BotAt(world, road, tunings[1], start, 1);
        var (group, bots) = Driving(world, track, tunings);

        var passedAt = -1f;
        var widest = 0f;
        var closest = float.MaxValue;
        for (var step = 0; step < 15 * 120 && passedAt < 0f; step++)
        {
            group.Update(Step);
            var gap = bots.GetTraffic(0).Driven - bots.GetTraffic(1).Driven;
            widest = MathF.Max(widest, MathF.Abs(bots.GetLaneOffset(1)));
            if (MathF.Abs(bots.GetTraffic(0).Lateral - bots.GetTraffic(1).Lateral) < 1.6f && gap > 0f)
                closest = MathF.Min(closest, gap);
            if (gap < -4f)
                passedAt = step * Step;
        }

        TestContext.Out.WriteLine($"straight of {straight:0} m; passed after {passedAt:0.0} s, lane up to " +
                                  $"{widest:0.0} m, closest behind {closest:0.0} m; slow kart " +
                                  $"{world.Get<Velocity>(slow).Value.Length():0} m/s, fast " +
                                  $"{world.Get<Velocity>(fast).Value.Length():0} m/s");
        Assert.That(passedAt, Is.InRange(0f, 10f), "passes within 10 s");
        Assert.That(widest, Is.GreaterThan(BotTraffic.LaneWidth * 0.8f), "changed lane");
        Assert.That(closest, Is.GreaterThan(tunings[0].ChassisLengthMeters), "never ran into it");
    }

    [Test]
    public void Follow_KartDirectlyAheadAndNoGap_BrakesInsteadOfRearEnding()
    {
        var track = StarCircuit.Value;
        var road = StarRoad.Value;
        using var world = new World();
        // Nine slow karts abreast, a lane apart, fill the 21 m road from edge margin to edge margin.
        const int wall = 9;
        const int middle = wall / 2;
        var tunings = new KartTuning[wall + 1];
        tunings.AsSpan().Fill(KartCatalog.Kart with { EnginePowerWatts = 8000f });
        tunings[wall] = KartCatalog.Kart;
        var start = LongestStraight(road, new BotRoutePlan(road, tunings[wall], 0.8f, 0.7f), out _);
        for (var i = 0; i < wall; i++)
            BotAt(world, road, tunings[i], start + 5, i, (i - middle) * BotTraffic.LaneWidth);
        var fast = BotAt(world, road, tunings[wall], start, wall, 0f);
        var (group, bots) = Driving(world, track, tunings);

        var braked = false;
        var closest = float.MaxValue;
        for (var step = 0; step < 8 * 120; step++)
        {
            group.Update(Step);
            closest = MathF.Min(closest, bots.GetTraffic(middle).Driven - bots.GetTraffic(wall).Driven);
            braked |= world.Get<KartControls>(fast).Brake > 0.2f;
        }

        TestContext.Out.WriteLine($"closest behind the middle kart {closest:0.0} m; fast kart " +
                                  $"{bots.GetTraffic(wall).Speed:0} m/s, wall {bots.GetTraffic(middle).Speed:0} m/s");
        Assert.That(braked, Is.True);
        Assert.That(closest, Is.GreaterThan(tunings[0].ChassisLengthMeters + 0.2f), "no rear-ending");
        Assert.That(bots.GetTraffic(wall).Speed, Is.EqualTo(bots.GetTraffic(middle).Speed).Within(2f), "follows");
    }

    [Test]
    public void Race_SixBotsFor90Seconds_OvertakeStayOnTheRoadAndDoNotGetStuck()
    {
        var track = StarCircuit.Value;
        using var world = new World();
        KartTuning[] catalog = [KartCatalog.Olabil, KartCatalog.Wagon, KartCatalog.Kart];
        var tunings = new KartTuning[6];
        for (var i = 0; i < tunings.Length; i++)
            tunings[i] = catalog[i % catalog.Length];
        var markers = RaceMarkers.Load(Path.Combine("Assets", "Tracks"), "star-circuit");
        var race = TrackScene.CreateSession(track, markers, laps: 5);
        var karts = TrackScene.CreateKarts(world, track, markers, tunings, playerIndex: -1, race);
        var (group, bots) = Driving(world, track, tunings, race);
        var road = bots.Road;
        var positions = new int[karts.Length];
        var reversals = new int[karts.Length];
        var reversing = new bool[karts.Length];
        var overtakes = 0;
        var offRoadLanes = 0;
        var allocated = 0L;
        var ticks = 0L;

        var steps = (int)((90 + RaceSession.CountdownSeconds) * 120);
        for (var step = 0; step < steps; step++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            group.Update(Step);
            if (step > (RaceSession.CountdownSeconds + 1) * 120) // plans are made on the first step
            {
                ticks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }

            for (var i = 0; i < karts.Length; i++)
            {
                var now = world.Get<Bot>(karts[i]).ReverseSeconds > 0f;
                if (now && !reversing[i])
                    reversals[i]++;
                reversing[i] = now;

                var position = world.Get<RaceProgress>(karts[i]).Position;
                if (positions[i] != 0 && position < positions[i])
                    overtakes++;
                positions[i] = position;

                var sample = bots.GetTraffic(i).Sample;
                var lane = bots.GetLaneOffset(i);
                if (sample >= 0 && (lane < -road.LeftWidth(sample) + 1f || lane > road.RightWidth(sample) - 1f))
                    offRoadLanes++;
            }
        }

        var microseconds = ticks * 1e6 / System.Diagnostics.Stopwatch.Frequency /
                           (steps - (RaceSession.CountdownSeconds + 1) * 120);
        TestContext.Out.WriteLine($"{overtakes} overtakes, {microseconds:0} µs per fixed step, {allocated} bytes");
        for (var i = 0; i < karts.Length; i++)
        {
            var progress = world.Get<RaceProgress>(karts[i]);
            TestContext.Out.WriteLine($"bot {i} (skill {world.Get<Bot>(karts[i]).Skill:0.0}): P{progress.Position}, " +
                                      $"{progress.CompletedLaps} laps, best {progress.BestLapSeconds:0.0} s, " +
                                      $"{reversals[i]} reversals");
            Assert.That(progress.CompletedLaps, Is.GreaterThanOrEqualTo(1), $"bot {i} drives on");
            Assert.That(progress.MissedCheckpoints, Is.Zero, $"bot {i} follows the route");
            Assert.That(reversals[i], Is.LessThanOrEqualTo(2), $"bot {i} got stuck");
        }

        Assert.That(overtakes, Is.GreaterThanOrEqualTo(1), "positions change");
        Assert.That(offRoadLanes, Is.Zero, "every lane stays on the asphalt");
        Assert.That(allocated, Is.Zero);
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static TrafficKart Kart(float along, float lateral, float speed) =>
        new() { Sample = (int)(along / BotRoad.SampleSpacing), Along = along, Lateral = lateral, Speed = speed };

    /// <summary>The bots, kart physics, contacts and ground, as <see cref="TrackScene.AddDriving"/>, no wander.</summary>
    private static (SystemGroup Group, BotDriverSystem Bots) Driving(World world, Track track, KartTuning[] tunings,
        RaceSession race = null)
    {
        var group = new SystemGroup(world);
        var physics = new KartPhysicsSystem(tunings);
        var bots = new BotDriverSystem(track, physics, race) { Wanders = race != null };
        group.Add(bots);
        group.Add(physics);
        bots.Contacts = group.Add(new ConsoleEngine.Physics.PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        group.Add(new KartGroundSystem(track));
        if (race != null)
            group.Add(new RaceSystem(race));
        return (group, bots);
    }

    /// <summary>A middling bot in slot <paramref name="index"/> at <paramref name="sample"/>, on its lane.</summary>
    private static Entity BotAt(World world, BotRoad road, KartTuning tuning, int sample, int index,
        float lane = 0f)
    {
        var forward = road.ForwardAt(sample);
        var point = road.PointAt(sample) + Right(forward) * lane;
        var kart = KartEntity.Create(world, tuning, new XnaVector2(point.X, point.Y),
            KartFrame.HeadingOf(new System.Numerics.Vector3(forward.X, 0f, forward.Y)), index);
        world.Add(kart, new Bot { Skill = 0.5f, LaneOffset = lane, Sample = -1 });
        return kart;
    }

    /// <summary>The first sample of the longest stretch at the plan's top speed, and its length.</summary>
    private static int LongestStraight(BotRoad road, BotRoutePlan plan, out float meters)
    {
        var top = 0f;
        for (var i = 0; i < road.Count; i++)
            top = MathF.Max(top, plan.SpeedAhead(i, 0f));

        var best = 0;
        var bestLength = 0;
        for (var i = 0; i < road.Count; i++)
        {
            var length = 0;
            while (length < road.Count && plan.SpeedAhead(i + length, 0f) >= top * 0.98f)
                length++;
            if (length > bestLength)
            {
                best = i;
                bestLength = length;
            }
        }

        meters = bestLength * road.Spacing;
        return best;
    }

    private static Vector2 Right(Vector2 forward) => new(-forward.Y, forward.X);

    private static bool IsOnRoad(Track track, Vector2 point) =>
        track.TryGetRoadHeight(new System.Numerics.Vector3(point.X, 0f, point.Y), out _);
}
