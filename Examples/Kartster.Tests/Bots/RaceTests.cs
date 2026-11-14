using System;
using System.IO;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Scenes;
using Kartster.Systems;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Tests.Bots;

/// <summary>Several karts on one track: the grid, who drives what, a headless race and karts hitting each other.</summary>
public class RaceTests
{
    private const float Step = TrackScene.StepSeconds;

    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static KartTuning[] Tunings(int count)
    {
        KartTuning[] catalog = [KartCatalog.Olabil, KartCatalog.Wagon, KartCatalog.Kart];
        var tunings = new KartTuning[count];
        for (var i = 0; i < count; i++)
            tunings[i] = catalog[i % catalog.Length];
        return tunings;
    }

    [Test]
    public void CreateKarts_SixKarts_HaveDistinctIndicesAndGridSlotsOnTheRoad()
    {
        var track = StarCircuit.Value;
        using var world = new World();

        var karts = TrackScene.CreateKarts(world, track, Tunings(6), playerIndex: 5);

        Assert.That(karts, Has.Length.EqualTo(6));
        for (var i = 0; i < karts.Length; i++)
        {
            Assert.That(world.Get<Kart>(karts[i]).Index, Is.EqualTo(i));
            Assert.That(world.Has<PlayerKart>(karts[i]), Is.EqualTo(i == 5));
            Assert.That(world.Has<Bot>(karts[i]), Is.EqualTo(i != 5));
            var position = world.Get<Position>(karts[i]).Value;
            Assert.That(track.TryGetRoadHeight(new System.Numerics.Vector3(position.X, 0f, position.Y), out _),
                Is.True, $"kart {i} starts on the road");
            for (var j = 0; j < i; j++)
                Assert.That(Vector2.Distance(position, world.Get<Position>(karts[j]).Value), Is.GreaterThan(3.9f));
        }

        Assert.That(world.Get<Bot>(karts[0]).Skill, Is.GreaterThan(world.Get<Bot>(karts[4]).Skill));
        Assert.That(world.Get<Bot>(karts[0]).LaneOffset, Is.Not.EqualTo(world.Get<Bot>(karts[1]).LaneOffset));
    }

    [Test]
    public void KartInputSystem_KeysHeld_WritesOnlyThePlayersControls()
    {
        using var world = new World();
        var karts = TrackScene.CreateKarts(world, StarCircuit.Value, Tunings(2), playerIndex: 1);
        var group = new SystemGroup(world);
        group.Add(new KartInputSystem(key => key is Key.W or Key.D));

        group.Update(0.05f);

        var player = world.Get<KartControls>(karts[1]);
        Assert.That(player.Throttle, Is.EqualTo(1f));
        Assert.That(player.Steer, Is.GreaterThan(0f));
        var bot = world.Get<KartControls>(karts[0]);
        Assert.That(bot.Throttle, Is.Zero);
        Assert.That(bot.Steer, Is.Zero);
    }

    [Test]
    public void Race_ThreeBotsFor60Seconds_EveryBotDrivesOnWithoutGettingStuck()
    {
        var track = StarCircuit.Value;
        using var world = new World();
        var tunings = Tunings(3);
        var karts = TrackScene.CreateKarts(world, track, tunings, playerIndex: -1);
        var group = new SystemGroup(world);
        TrackScene.AddDriving(group, track, tunings);
        var reversals = new int[karts.Length];
        var reversing = new bool[karts.Length];

        for (var step = 0; step < 60 * 120; step++)
        {
            group.Update(Step);
            for (var i = 0; i < karts.Length; i++)
            {
                var now = world.Get<Bot>(karts[i]).ReverseSeconds > 0f;
                if (now && !reversing[i])
                    reversals[i]++;
                reversing[i] = now;
            }
        }

        for (var i = 0; i < karts.Length; i++)
        {
            var bot = world.Get<Bot>(karts[i]);
            TestContext.Out.WriteLine($"bot {i} ({tunings[i].Mass} kg, skill {bot.Skill:0.0}): {bot.Progress:0} m, " +
                                      $"{bot.Progress / 60f * 3.6f:0} km/h average, " +
                                      $"lap {track.Length / bot.Progress * 60f:0.0} s");
            Assert.That(bot.Progress, Is.GreaterThan(500f), $"bot {i} drives on");
            Assert.That(reversals[i], Is.LessThanOrEqualTo(2), $"bot {i} got stuck");
        }
    }

    [Test]
    public void Collision_TwoKartsHeadOn_DoNotGainEnergy()
    {
        using var world = new World();
        var tuning = KartCatalog.Kart;
        var a = KartEntity.Create(world, tuning, new Vector2(0f, -4f), 0f, 0);
        var b = KartEntity.Create(world, tuning, new Vector2(0.3f, 4f), MathF.PI, 1);
        world.Get<Velocity>(a).Value = new Vector2(0f, 8f);
        world.Get<Velocity>(b).Value = new Vector2(0f, -8f);
        var group = new SystemGroup(world);
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        var before = Energy(world, a, tuning) + Energy(world, b, tuning);

        var most = 0f;
        for (var step = 0; step < 240; step++)
        {
            group.Update(Step);
            most = MathF.Max(most, Energy(world, a, tuning) + Energy(world, b, tuning));
        }

        Assert.That(most, Is.LessThanOrEqualTo(before * 1.001f));
        Assert.That(world.Get<Position>(b).Value.Y - world.Get<Position>(a).Value.Y, Is.GreaterThan(2f),
            "they bounce apart, not through each other");
    }

    [Test]
    public void Collision_KartPushingAnother_StaysCalm()
    {
        using var world = new World();
        var tunings = new[] { KartCatalog.Kart, KartCatalog.Kart };
        var front = KartEntity.Create(world, tunings[0], new Vector2(0f, -3.2f), 0f, 0);
        var back = KartEntity.Create(world, tunings[1], new Vector2(0f, 0f), 0f, 1);
        var group = new SystemGroup(world);
        var physics = new KartPhysicsSystem(tunings);
        group.Add(physics);
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        world.Get<KartControls>(back) = new KartControls { Throttle = 1f };
        world.Get<KartControls>(front) = new KartControls { Brake = 0.3f };

        var fastest = 0f;
        var mostSpin = 0f;
        for (var step = 0; step < 5 * 120; step++)
        {
            group.Update(Step);
            fastest = MathF.Max(fastest, world.Get<Velocity>(front).Value.Length());
            mostSpin = MathF.Max(mostSpin, MathF.Abs(world.Get<AngularVelocity>(front).Value));
        }

        var gap = Vector2.Distance(world.Get<Position>(front).Value, world.Get<Position>(back).Value);
        Assert.That(float.IsFinite(gap), Is.True);
        Assert.That(fastest, Is.LessThan(physics.GetDynamics(1).TopSpeed));
        Assert.That(mostSpin, Is.LessThan(3f), "no wild spin from the contact");
        Assert.That(gap, Is.GreaterThan(tunings[0].ChassisLengthMeters * 0.9f), "no sinking into each other");
    }

    [Test]
    public void FixedStep_SixKartsInSteadyState_DoesNotAllocate()
    {
        var track = StarCircuit.Value;
        using var world = new World();
        var tunings = Tunings(6);
        var karts = TrackScene.CreateKarts(world, track, tunings, playerIndex: 5);
        var group = new SystemGroup(world);
        TrackScene.AddDriving(group, track, tunings);
        world.Get<KartControls>(karts[5]) = new KartControls { Throttle = 1f, Steer = 0.2f };
        for (var step = 0; step < 240; step++)
            group.Update(Step);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var step = 0; step < 240; step++)
            group.Update(Step);
        var microseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMicroseconds / 240;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.Out.WriteLine($"{microseconds:0} µs per fixed step with 6 karts");
        Assert.That(allocated, Is.Zero);
    }

    private static float Energy(World world, Entity kart, KartTuning tuning)
    {
        var speed = world.Get<Velocity>(kart).Value.Length();
        var spin = world.Get<AngularVelocity>(kart).Value;
        var inertia = tuning.Mass * (tuning.ChassisWidthMeters * tuning.ChassisWidthMeters +
                                     tuning.ChassisLengthMeters * tuning.ChassisLengthMeters) / 12f;
        return 0.5f * tuning.Mass * speed * speed + 0.5f * inertia * spin * spin;
    }
}
