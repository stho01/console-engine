using System;
using System.IO;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;
using Kartster.PowerUps;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Systems;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Tests.PowerUps;

/// <summary>Power-ups on the ECS karts: effects in the kart model, bots pressing use, keys, a whole race.</summary>
public class PowerUpSystemTests
{
    private const float Step = TrackScene.StepSeconds;

    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    /// <summary>Karts 40 m apart side by side facing -Z, the kart model, the physics and the power-ups.</summary>
    private static (SystemGroup Group, PowerUpField Field, Entity[] Karts) Karts(World world, int count, int bots = 0)
    {
        var tunings = new KartTuning[count];
        var karts = new Entity[count];
        for (var i = 0; i < count; i++)
        {
            tunings[i] = KartCatalog.Kart;
            karts[i] = KartEntity.Create(world, tunings[i], new Vector2(i * 40f, 0f), 0f, i);
            if (i < bots)
                world.Add(karts[i], new Bot());
        }

        var group = new SystemGroup(world);
        var physics = new KartPhysicsSystem(tunings);
        var field = new PowerUpField([], count, seed: 1);
        physics.Effects = new KartEffectTuning(field);
        group.Add(physics);
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        group.Add(new PowerUpSystem(field, physics));
        return (group, field, karts);
    }

    private static float Speed(World world, Entity kart) => world.Get<Velocity>(kart).Value.Length();

    [Test]
    public void Build_SpeedBoost_IsKartstersPowerAndGripOnTheKartsOwnTuning()
    {
        var dynamics = new KartDynamics(KartCatalog.Kart);

        var boosted = KartEffectTuning.Build(dynamics, PowerUpCatalog.BoostPower, PowerUpCatalog.BoostGrip,
            Surface.Asphalt, isBoosting: true);

        Assert.That(boosted.EnginePowerWatts, Is.EqualTo(KartCatalog.Kart.EnginePowerWatts * 1.6f).Within(0.01f));
        Assert.That(boosted.TyreFriction, Is.EqualTo(KartCatalog.Kart.TyreFriction * 1.2f).Within(1e-4f));
        Assert.That(boosted.TractionFriction, Is.EqualTo(KartCatalog.Kart.TractionFriction * 1.2f).Within(1e-4f));
        Assert.That(KartEngine.GetTopSpeed(boosted), Is.GreaterThan(dynamics.TopSpeed * 1.1f));
    }

    [Test]
    public void Build_SpeedBoostOnGrass_GovernsToNineTenthsInsteadOfHalf()
    {
        var dynamics = new KartDynamics(KartCatalog.Kart);

        var boosted = KartEffectTuning.Build(dynamics, 1.6f, 1.2f, Surface.Grass, isBoosting: true);
        var tuned = KartEffectTuning.Build(dynamics, 1.35f, 1f, Surface.Grass, isBoosting: false);

        Assert.That(KartEngine.GetTopSpeed(boosted), Is.EqualTo(dynamics.TopSpeed * 0.9f).Within(0.05f));
        Assert.That(KartEngine.GetTopSpeed(tuned), Is.EqualTo(dynamics.TopSpeed * 0.5f).Within(0.05f));
    }

    [Test]
    public void EngineTune_FullThrottleFromStandstill_PullsAwayFromTheSameKart()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, 2);
        world.Get<KartControls>(karts[0]) = new KartControls { Throttle = 1f };
        world.Get<KartControls>(karts[1]) = new KartControls { Throttle = 1f };
        field.Give(0, PowerUpKind.EngineTune);
        field.RequestUse(0);

        for (var step = 0; step < 3 * 120; step++)
            group.Update(Step);

        TestContext.Out.WriteLine($"tuned {Speed(world, karts[0]):0.0} m/s, plain {Speed(world, karts[1]):0.0} m/s");
        Assert.That(Speed(world, karts[0]), Is.GreaterThan(Speed(world, karts[1]) * 1.05f));
    }

    [Test]
    public void SpeedBoost_InTheRace_AddsNineMetresPerSecondAtOnce()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, 1);
        world.Get<Velocity>(karts[0]).Value = new Vector2(0f, -10f);
        field.Give(0, PowerUpKind.SpeedBoost);
        field.RequestUse(0);

        group.Update(Step);

        Assert.That(Speed(world, karts[0]), Is.EqualTo(19f).Within(0.3f));
    }

    [Test]
    public void Banana_KartAtSpeedDrivesOverIt_SpinsOut()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, 2);
        // Kart 1 faces -Z 30 m behind kart 0's banana, which kart 0 drops facing +Z.
        world.Get<Rotation>(karts[0]).Radians = KartFrame.ToRadians(MathF.PI);
        world.Get<Position>(karts[1]).Value = new Vector2(0f, 30f);
        field.Give(0, PowerUpKind.Banana);
        field.RequestUse(0);
        group.Update(Step);
        world.Get<Position>(karts[0]).Value = new Vector2(-40f, 0f);
        world.Get<Velocity>(karts[1]).Value = new Vector2(0f, -20f);
        world.Get<KartControls>(karts[1]) = new KartControls { Throttle = 1f };

        var hitAt = -1;
        for (var step = 0; step < 4 * 120; step++)
        {
            group.Update(Step);
            if (hitAt < 0 && field.Hazards.Length == 0)
                hitAt = step;
        }

        var heading = KartFrame.ToHeading(world.Get<Rotation>(karts[1]).Radians);
        TestContext.Out.WriteLine($"hit at step {hitAt}, heading {heading * 180f / MathF.PI:0}°");
        Assert.That(hitAt, Is.GreaterThan(0));
        Assert.That(MathF.Abs(MathF.IEEERemainder(heading, MathF.Tau)), Is.GreaterThan(0.5f), "spun off its line");
    }

    [Test]
    public void Bot_HoldingAGripTune_UsesItAtOnce()
    {
        using var world = new World();
        var (group, field, _) = Karts(world, 2, bots: 1);
        field.Give(0, PowerUpKind.GripTune);

        group.Update(Step);

        Assert.That(field.Held(0), Is.EqualTo(PowerUpKind.None));
        Assert.That(field.Effects(0).TyreGrip, Is.EqualTo(PowerUpCatalog.GripTuneGrip));
    }

    [Test]
    public void Bot_HoldingBananas_DropsOnlyWithAKartCloseBehind()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, 2, bots: 1);
        field.Give(0, PowerUpKind.Banana);

        group.Update(Step);
        Assert.That(field.Charges(0), Is.EqualTo(5), "the other kart is beside it, not behind");

        world.Get<Position>(karts[1]).Value = new Vector2(0f, 10f); // 10 m behind (it faces -Z)
        group.Update(Step);
        Assert.That(field.Charges(0), Is.EqualTo(4));
        Assert.That(field.Hazards.Length, Is.EqualTo(1));

        group.Update(Step);
        Assert.That(field.Charges(0), Is.EqualTo(4), "one press every 0.4 s");
    }

    [Test]
    public void PowerUpInputSystem_TPressed_ThrowsTheBananaAhead()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, 1);
        world.Add(karts[0], new PlayerKart());
        var frame = new SystemGroup(world);
        frame.Add(new PowerUpInputSystem(field, key => key == Key.T));
        field.Give(0, PowerUpKind.Banana);

        frame.Update(0.016f);
        group.Update(Step);

        Assert.That(field.Hazards.Length, Is.EqualTo(1));
        Assert.That(field.Hazards[0].InAir, Is.True);
        Assert.That(field.Hazards[0].Velocity.Y, Is.LessThan(-20f), "thrown ahead, towards -Z");
    }

    [Test]
    public void PowerUpPads_StarCircuit_HasKartstersNinePadsAndNoStaticBoxes()
    {
        var track = StarCircuit.Value;

        Assert.That(track.PowerUpPads, Has.Length.EqualTo(9));
        Assert.That(track.PowerUpPads[0], Is.EqualTo(new System.Numerics.Vector3(55.651f, 1f, -84.84f)));
        Assert.That(File.ReadAllText(Path.Combine("Assets", "Tracks", "star-circuit.scenery.txt")),
            Does.Not.Contain("model Box"), "the pads are drawn by the power-ups, not as scenery");
    }

    [Test]
    public void PowerUpPads_BadLine_SaysWhereAndWhat()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            PowerUpPads.Parse(["# pads", "pad 1 2 three normal"], "test.powerups.txt"));

        Assert.That(error.Message, Is.EqualTo("test.powerups.txt:2: expected 'pad x y z setId'"));
    }

    [Test]
    public void Race_SixKartsOnStarCircuit_BotsTakePadsAndUseThem()
    {
        var track = StarCircuit.Value;
        using var world = new World();
        var tunings = new KartTuning[6];
        Array.Fill(tunings, KartCatalog.Kart);
        var markers = RaceMarkers.Load(Path.Combine("Assets", "Tracks"), "star-circuit");
        var race = TrackScene.CreateSession(track, markers, laps: 3);
        TrackScene.CreateKarts(world, track, markers, tunings, playerIndex: -1, race);
        var group = new SystemGroup(world);
        var physics = TrackScene.AddDriving(group, track, tunings, race);
        var field = PowerUpSystem.Add(group, null, track, physics, race, seed: 5).Field;

        var taken = 0;
        var used = 0;
        var wasSpinning = new bool[6];
        var lastCharges = new int[6];
        for (var step = 0; step < (40 + RaceSession.CountdownSeconds) * 120; step++)
        {
            group.Update(Step);
            for (var k = 0; k < 6; k++)
            {
                var spinning = field.IsSpinning(k);
                var charges = field.Charges(k);
                if (spinning && !wasSpinning[k])
                    taken++;
                if (!spinning && !wasSpinning[k] && charges < lastCharges[k])
                    used++;
                wasSpinning[k] = spinning;
                lastCharges[k] = charges;
            }
        }

        TestContext.Out.WriteLine($"{taken} pads taken, {used} uses, {field.Hazards.Length} hazards on the road");
        Assert.That(taken, Is.GreaterThanOrEqualTo(3));
        Assert.That(used, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void FixedStep_SixKartsWithPowerUpsInSteadyState_DoesNotAllocate()
    {
        var track = StarCircuit.Value;
        using var world = new World();
        var tunings = new KartTuning[6];
        Array.Fill(tunings, KartCatalog.Kart);
        var karts = TrackScene.CreateKarts(world, track, null, tunings, playerIndex: 5);
        var group = new SystemGroup(world);
        var physics = TrackScene.AddDriving(group, track, tunings);
        var field = PowerUpSystem.Add(group, null, track, physics, seed: 1).Field;
        world.Get<KartControls>(karts[5]) = new KartControls { Throttle = 1f, Steer = 0.2f };
        for (var k = 0; k < 6; k++)
        {
            field.Give(k, PowerUpKind.EngineTune);
            field.RequestUse(k);
        }

        for (var step = 0; step < 240; step++)
            group.Update(Step);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var step = 0; step < 240; step++)
            group.Update(Step);
        var microseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMicroseconds / 240;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.Out.WriteLine($"{microseconds:0} µs per fixed step with 6 karts and power-ups");
        Assert.That(allocated, Is.Zero);
    }
}
