using System;
using System.IO;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Bots;
using Kartster.Components;
using Kartster.Driving;
using Kartster.PowerUps;
using Kartster.Scenes;
using Kartster.Systems;
using NUnit.Framework;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Tests.PowerUps;

/// <summary>
/// The power-ups after the first eight, with Kartster's numbers: invulnerability, the magnet, the anvil, lightning,
/// the vampire, earthquake and glitch, the smoke bomb, the fakes, roadblocks and the locomotive; their bots.
/// </summary>
public class PowerUpKindsTests
{
    private const float Step = 1f / 120f;

    private static readonly Lazy<BotRoutePlan> StarCircuitRoute = new(() => new BotRoutePlan(
        Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"), KartCatalog.Kart,
        BotDriverSystem.CornerSpeedFactor(0.5f), BotDriverSystem.BrakingShare(0.5f)));

    private static PowerUpKart Kart(float x, float z, float heading = 0f, float speed = 0f, int position = 1)
    {
        var kart = new PowerUpKart
        {
            Position = new Vector2(x, z), Heading = heading, RacePosition = position, HalfWidth = 0.8f,
            HalfLength = 1.4f,
        };
        kart.Velocity = kart.Forward * speed;
        return kart;
    }

    /// <summary>A kart on Star Circuit's centerline at route sample <paramref name="sample"/>, facing along it.</summary>
    private static PowerUpKart OnRoad(BotRoad road, int sample, int position, float lateral = 0f)
    {
        var forward = road.ForwardAt(sample);
        var point = road.PointAhead(sample, 0f, lateral);
        return Kart(point.X, point.Y, MathF.Atan2(-forward.X, -forward.Y), position: position);
    }

    private static void Run(PowerUpField field, PowerUpKart[] karts, float seconds)
    {
        for (var t = 0f; t < seconds; t += Step)
        {
            for (var k = 0; k < karts.Length; k++)
            {
                karts[k].Position += karts[k].Velocity * Step;
                karts[k].Heading += karts[k].YawRate * Step;
            }

            field.Step(Step, karts);
        }
    }

    private static void Use(PowerUpField field, PowerUpKart[] karts, int kart, PowerUpKind kind,
        PowerUpAim aim = PowerUpAim.Default)
    {
        field.Give(kart, kind);
        field.RequestUse(kart, aim);
        field.Step(Step, karts);
    }

    //**********************************************************
    //** invulnerability
    //**********************************************************

    [Test]
    public void Invulnerable_DrivesOverABanana_IsNotSpunAndTheBananaStaysUntilItRunsOut()
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f), Kart(0f, -10f)];
        Use(field, karts, 1, PowerUpKind.Banana); // lies at (0, -7.5)
        karts[1].Position = new Vector2(40f, 0f);
        Use(field, karts, 0, PowerUpKind.Invulnerable);
        Assert.That(field.Effects(0).IsInvulnerable, Is.True);

        karts[0].Velocity = new Vector2(0f, -10f);
        Run(field, karts, 1.5f);
        Assert.That(karts[0].YawRate, Is.Zero, "not spun");
        Assert.That(field.Hazards.Length, Is.EqualTo(1), "still armed for whoever is behind");

        Run(field, karts, PowerUpCatalog.InvulnerableSeconds - 1.5f);
        Assert.That(field.Effects(0).IsInvulnerable, Is.False, "5 s");
        karts[0].Position = new Vector2(0f, 0f);
        Run(field, karts, 1.5f);
        Assert.That(karts[0].YawRate, Is.EqualTo(PowerUpCatalog.BananaSpin), "hit once it ran out");
        Assert.That(field.Hazards.Length, Is.Zero);
    }

    [Test]
    public void Invulnerable_LapskausFiredAtIt_FliesThroughAndHitsNobody()
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f), Kart(0f, 20f)];
        Use(field, karts, 0, PowerUpKind.Invulnerable);
        Use(field, karts, 1, PowerUpKind.Lapskaus); // fired along -Z at kart 0

        Run(field, karts, 0.5f);

        Assert.That(karts[0].Velocity, Is.EqualTo(Vector2.Zero));
        Assert.That(karts[0].YawRate, Is.Zero);
        Assert.That(field.Projectiles.Length, Is.EqualTo(1), "flies on");
        Assert.That(field.Projectiles[0].Position.Y, Is.LessThan(-1f), "past the kart");
    }

    [Test]
    public void Invulnerable_HomingJoikaChasingIt_NeverHitsUntilItRunsOut()
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f, position: 1), Kart(0f, 30f, position: 2)];
        Use(field, karts, 0, PowerUpKind.Invulnerable);
        Use(field, karts, 1, PowerUpKind.HomingJoika);

        Run(field, karts, 4f);
        Assert.That(karts[0].YawRate, Is.Zero, "chased, never hit");
        Assert.That(field.Projectiles.Length, Is.EqualTo(1));

        Run(field, karts, 3f);
        Assert.That(karts[0].YawRate, Is.Not.Zero, "hit once invulnerability is over (5 s), within the can's 8 s");
    }

    [Test]
    public void ContactMass_AnvilAndInvulnerable_AreFourAndImmovable()
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f), Kart(40f, 0f)];
        Use(field, karts, 0, PowerUpKind.Anvil);
        Use(field, karts, 1, PowerUpKind.Invulnerable);

        Assert.That(field.ContactMass(0), Is.EqualTo(4f));
        Assert.That(field.ContactMass(1), Is.EqualTo(PowerUpCatalog.ImmovableMass));
        Run(field, karts, 8f);
        Assert.That(field.ContactMass(0), Is.EqualTo(1f), "8 s");
        Assert.That(field.ContactMass(1), Is.EqualTo(1f), "5 s");
    }

    //**********************************************************
    //** effects on karts
    //**********************************************************

    [Test]
    public void Magnet_Used_PullsKartsWithin26MetresAtFourteenButNotTheInvulnerable()
    {
        var field = new PowerUpField([], 4, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f), Kart(10f, 0f), Kart(30f, 0f), Kart(0f, 10f)];
        Use(field, karts, 3, PowerUpKind.Invulnerable);
        Use(field, karts, 0, PowerUpKind.Magnet);

        Run(field, karts, 0.5f);

        Assert.That(karts[1].Velocity.X, Is.EqualTo(-PowerUpCatalog.MagnetPull * 0.5f).Within(0.4f), "14 m/s²");
        Assert.That(karts[2].Velocity, Is.EqualTo(Vector2.Zero), "out of reach");
        Assert.That(karts[3].Velocity, Is.EqualTo(Vector2.Zero), "invulnerable");
        Assert.That(karts[0].Velocity, Is.EqualTo(Vector2.Zero), "not itself");
    }

    [Test]
    public void Vampire_FromSecondPlace_TakesAQuarterOfTheLeadersPowerForEightSeconds()
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, -20f, position: 1), Kart(0f, 0f, position: 2)];

        Use(field, karts, 1, PowerUpKind.Vampire);

        Assert.That(field.Effects(0).EnginePower, Is.EqualTo(0.75f));
        Assert.That(field.Effects(1).EnginePower, Is.EqualTo(1.25f));
        Run(field, karts, PowerUpCatalog.VampireSeconds);
        Assert.That(field.Effects(0).EnginePower, Is.EqualTo(1f));
        Assert.That(field.Effects(1).EnginePower, Is.EqualTo(1f));
    }

    [Test]
    public void Vampire_FromTheLead_DoesNothingAndIsSpent()
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, -20f, position: 1), Kart(0f, 0f, position: 2)];

        Use(field, karts, 0, PowerUpKind.Vampire);

        Assert.That(field.Held(0), Is.EqualTo(PowerUpKind.None));
        Assert.That(field.Effects(0).Count + field.Effects(1).Count, Is.Zero);
    }

    [Test]
    public void Lightning_Used_HoldsEveryOtherKartToTwoThirdsAndHalfSizeForSixSeconds()
    {
        var field = new PowerUpField([], 3, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f), Kart(10f, 0f), Kart(20f, 0f)];
        Use(field, karts, 2, PowerUpKind.Invulnerable); // not a cleanse: struck all the same

        Use(field, karts, 0, PowerUpKind.Lightning);

        Assert.That(field.Effects(0).TopSpeed, Is.EqualTo(1f));
        for (var k = 1; k < 3; k++)
        {
            Assert.That(field.Effects(k).TopSpeed, Is.EqualTo(0.667f));
            Assert.That(field.Effects(k).DrawScale, Is.EqualTo(0.5f));
        }

        Run(field, karts, PowerUpCatalog.LightningSeconds);
        Assert.That(field.Effects(1).TopSpeed, Is.EqualTo(1f));
    }

    [Test]
    public void Build_Lightning_GovernsToTwoThirdsOfTopSpeed()
    {
        var dynamics = new KartDynamics(KartCatalog.Kart);

        var struck = KartEffectTuning.Build(dynamics, 1f, 1f, Surface.Asphalt, isBoosting: false,
            topSpeedMultiplier: PowerUpCatalog.LightningTopSpeed);

        Assert.That(KartEngine.GetTopSpeed(struck), Is.EqualTo(dynamics.TopSpeed * 0.667f).Within(0.05f));
    }

    [Test]
    public void GlitchAndEarthquake_Used_SwapAndShakeEveryOtherKartsSteeringForFiveSeconds()
    {
        var field = new PowerUpField([], 3, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f), Kart(10f, 0f), Kart(20f, 0f)];
        Use(field, karts, 0, PowerUpKind.Glitch);
        Use(field, karts, 2, PowerUpKind.Earthquake);

        Assert.That(field.Steer(2, 0.5f), Is.EqualTo(-0.5f), "kart 2 was glitched only");
        Assert.That(field.Steer(1, 0.5f), Is.InRange(-0.8f, -0.2f), "kart 1 both: swapped, then shaken");
        float lowest = 1f, highest = -1f;
        for (var i = 0; i < 400; i++)
        {
            var steer = field.Steer(0, 0.2f); // kart 0 only felt the earthquake
            lowest = MathF.Min(lowest, steer);
            highest = MathF.Max(highest, steer);
        }

        Assert.That(lowest, Is.LessThan(0f).And.GreaterThanOrEqualTo(0.2f - 0.3f - 1e-5f));
        Assert.That(highest, Is.GreaterThan(0.4f).And.LessThanOrEqualTo(0.2f + 0.3f + 1e-5f));
        Run(field, karts, PowerUpCatalog.DisruptionSeconds);
        Assert.That(field.Steer(1, 0.5f), Is.EqualTo(0.5f));
        Assert.That(field.Steer(0, 0.2f), Is.EqualTo(0.2f));
    }

    //**********************************************************
    //** what is left on the road
    //**********************************************************

    [Test]
    public void FakePowerUp_DrivenInto_StopsTheKartDeadAndTakesItsGrip()
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 5f), Kart(0f, -10f)];
        Use(field, karts, 1, PowerUpKind.FakePowerUp); // at (0, -7.5), armed after 1 s
        karts[1].Position = new Vector2(40f, 0f);
        karts[0].Velocity = new Vector2(0f, -10f);

        Run(field, karts, 2f);

        Assert.That(field.Hazards.Length, Is.Zero, "used up");
        Assert.That(karts[0].Velocity, Is.EqualTo(Vector2.Zero));
        Assert.That(karts[0].YawRate, Is.EqualTo(PowerUpCatalog.CanSpin));
        Assert.That(field.Effects(0).TyreGrip, Is.LessThan(1f));
    }

    [Test]
    public void SmokeBomb_FuseRunsOut_BlastsKartsWithinEightMetresThenLeavesACloudForTenSeconds()
    {
        var field = new PowerUpField([], 3, seed: 1);
        PowerUpKart[] karts = [Kart(4f, -7.5f), Kart(0f, -10f), Kart(20f, -7.5f)];
        Use(field, karts, 1, PowerUpKind.SmokeBomb); // at (0, -7.5)
        karts[1].Position = new Vector2(-40f, 0f);

        Run(field, karts, PowerUpCatalog.SmokeFuseSeconds + 0.05f);

        // 4 m from the middle: half strength, a 5 m/s shove away from it, half the spin.
        Assert.That(karts[0].Velocity.X, Is.EqualTo(5f).Within(0.01f));
        Assert.That(karts[0].YawRate, Is.EqualTo(2.5f).Within(0.01f));
        Assert.That(field.Effects(0).TyreGrip, Is.LessThan(0.5f));
        Assert.That(karts[2].Velocity, Is.EqualTo(Vector2.Zero), "out of the blast");
        Run(field, karts, 1.5f);
        Assert.That(field.SmokeCover(new Vector2(0f, -7.5f)), Is.EqualTo(PowerUpCatalog.SmokeDarkness).Within(0.01f));
        Assert.That(field.Hazards.Length, Is.EqualTo(1));
        Run(field, karts, PowerUpCatalog.SmokeSeconds - 2.3f);
        Assert.That(field.Hazards.Length, Is.Zero, "10.8 s");
    }

    [TestCase(PowerUpKind.ConcreteRoadblock, 20f, 0f)]
    [TestCase(PowerUpKind.Roadblock, 20f, 15f)]
    [TestCase(PowerUpKind.ConcreteRoadblock, 3f, 3f)]
    public void Roadblock_KartDrivesAtIt_CrashesAboveFourMetresPerSecond(PowerUpKind kind, float speed, float after)
    {
        var field = new PowerUpField([], 2, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f), Kart(0f, -10f)];
        Use(field, karts, 1, kind); // across the road at (0, -6.5)
        karts[1].Position = new Vector2(40f, 0f);
        Assert.That(field.Obstacles[0].IsActive, Is.True);
        Assert.That(field.Obstacles[0].Position.Y, Is.EqualTo(-6.5f).Within(1e-4f));
        karts[0].Velocity = new Vector2(0f, -speed);

        Run(field, karts, 0.5f);

        Assert.That(karts[0].Velocity.Length(), Is.EqualTo(after).Within(1e-3f));
    }

    [Test]
    public void Roadblock_ThirtySeconds_IsGoneButConcreteStays()
    {
        var field = new PowerUpField([], 1, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f)];
        Use(field, karts, 0, PowerUpKind.Roadblock);
        Use(field, karts, 0, PowerUpKind.ConcreteRoadblock);

        Run(field, karts, PowerUpCatalog.RoadblockSeconds);

        Assert.That(field.Obstacles[0].IsActive, Is.False);
        Assert.That(field.Obstacles[1].IsActive, Is.True);
    }

    [Test]
    public void Roadblock_Thrown_LandsAsABlockAcrossTheThrow()
    {
        var field = new PowerUpField([], 1, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f)];
        Use(field, karts, 0, PowerUpKind.Roadblock, PowerUpAim.Forward);
        Assert.That(field.Hazards[0].InAir, Is.True);

        Run(field, karts, 2.5f);

        Assert.That(field.Hazards.Length, Is.Zero);
        Assert.That(field.Obstacles[0].IsActive, Is.True);
        Assert.That(field.Obstacles[0].Position.Y, Is.LessThan(-40f), "about 48 m ahead");
    }

    [Test]
    public void FakeFinish_OnTheRoad_IsPaintedAcrossItAndOffTheRoadIsSpent()
    {
        var route = StarCircuitRoute.Value;
        var field = new PowerUpField([], 2, seed: 1, route);
        PowerUpKart[] karts = [OnRoad(route.Road, 50, 1), OnRoad(route.Road, 200, 2, lateral: 40f)];

        Use(field, karts, 0, PowerUpKind.FakeFinish);
        Use(field, karts, 1, PowerUpKind.FakeFinish);

        Assert.That(field.Decoys.Length, Is.EqualTo(1));
        Assert.That(field.Decoys[0].LeftWidth + field.Decoys[0].RightWidth, Is.GreaterThan(15f), "the road's width");
        Assert.That(field.Held(1), Is.EqualTo(PowerUpKind.None), "spent off the road");
    }

    //**********************************************************
    //** the locomotive
    //**********************************************************

    [Test]
    public void Locomotive_SentAhead_HitsTheKartInItsWayOnceThenFadesPastTheLeader()
    {
        var route = StarCircuitRoute.Value;
        var field = new PowerUpField([], 2, seed: 1, route);
        PowerUpKart[] karts = [OnRoad(route.Road, 150, 1), OnRoad(route.Road, 100, 2)];

        Use(field, karts, 1, PowerUpKind.Locomotive);
        Assert.That(field.Trains.Length, Is.EqualTo(1));
        Assert.That(field.Trains[0].IsFading, Is.False);

        var hitAt = -1f;
        for (var t = 0f; t < 6f && field.Trains.Length > 0; t += Step)
        {
            Run(field, karts, Step);
            if (hitAt < 0f && karts[0].Velocity != Vector2.Zero)
                hitAt = t;
        }

        TestContext.Out.WriteLine($"hit after {hitAt:0.00} s, speed {karts[0].Velocity.Length():0.0} m/s");
        Assert.That(hitAt, Is.InRange(1f, 2.5f), "100 m to catch up at 70 m/s");
        Assert.That(karts[0].Velocity.Length(), Is.EqualTo(MathF.Sqrt(22f * 22f + 12f * 12f)).Within(0.5f));
        Assert.That(karts[1].Velocity, Is.EqualTo(Vector2.Zero), "the owner was 4 m clear");
        Assert.That(field.Trains.Length, Is.Zero, "faded 40 m past the leader");
    }

    [Test]
    public void Locomotive_SentByTheLeader_HasNobodyToPassAndFadesAtOnce()
    {
        var route = StarCircuitRoute.Value;
        var field = new PowerUpField([], 2, seed: 1, route);
        PowerUpKart[] karts = [OnRoad(route.Road, 150, 1), OnRoad(route.Road, 100, 2)];

        Use(field, karts, 0, PowerUpKind.Locomotive);

        Assert.That(field.Trains[0].IsFading, Is.True);
        Run(field, karts, PowerUpCatalog.TrainFadeSeconds);
        Assert.That(field.Trains.Length, Is.Zero);
        Assert.That(karts[1].Velocity, Is.EqualTo(Vector2.Zero), "a fading train hits nobody");
    }

    //**********************************************************
    //** the draw and the bots
    //**********************************************************

    [Test]
    public void Roll_LastKart_DrawsTheNewKindsAtKartstersWeights()
    {
        var random = new Random(7);
        const int draws = 40000;
        int locomotives = 0, magnets = 0, invulnerable = 0, smoke = 0;
        for (var i = 0; i < draws; i++)
        {
            var last = PowerUpCatalog.Roll(1f, random);
            if (last == PowerUpKind.Locomotive) locomotives++;
            if (last == PowerUpKind.Magnet) magnets++;
            var leader = PowerUpCatalog.Roll(0f, random);
            if (leader == PowerUpKind.Invulnerable) invulnerable++;
            if (leader == PowerUpKind.SmokeBomb) smoke++;
            Assert.That(leader, Is.Not.EqualTo(PowerUpKind.Locomotive).And.Not.EqualTo(PowerUpKind.Lightning));
        }

        Assert.That(locomotives / (float)draws, Is.EqualTo(2.5f / 32.1f).Within(0.01f));
        Assert.That(magnets / (float)draws, Is.EqualTo(2f / 32.1f).Within(0.01f));
        Assert.That(invulnerable / (float)draws, Is.EqualTo(1.5f / 21.5f).Within(0.01f));
        Assert.That(smoke / (float)draws, Is.EqualTo(2f / 21.5f).Within(0.01f));
        Assert.That(PowerUpCatalog.WeightAt(PowerUpKind.Roadblock, 0.5f), Is.EqualTo(1.5f));
    }

    [Test]
    public void Draw_SixKartsSeeded_GivesTheLeaderNoOffensiveKinds()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, 0f)], 6, seed: 11);
        var karts = new PowerUpKart[6];
        for (var k = 0; k < 6; k++)
            karts[k] = Kart(100f * k, 0f, position: k + 1);

        field.Step(Step, karts);
        for (var face = 0; face < PowerUpCatalog.ReelLength; face++)
        {
            Run(field, karts, PowerUpCatalog.FastFaceSeconds);
            Assert.That(field.Showing(0), Is.Not.AnyOf(PowerUpKind.Lightning, PowerUpKind.Magnet, PowerUpKind.Vampire,
                PowerUpKind.Earthquake, PowerUpKind.Glitch, PowerUpKind.Locomotive, PowerUpKind.Lapskaus));
        }
    }

    private static bool BotUses(PowerUpKind kind, PowerUpKart[] karts, bool armedBananaAhead = false)
    {
        var field = new PowerUpField([], karts.Length, seed: 1);
        if (armedBananaAhead)
        {
            var dropper = Kart(0f, -17.5f);
            PowerUpKart[] placing = [dropper, .. karts[1..]];
            Use(field, placing, 0, PowerUpKind.Banana); // lies 20 m ahead of kart 0
            Run(field, placing, 0.5f);
        }

        field.Give(0, kind);
        new BotPowerUps(karts.Length).Update(field, karts, 0, null, Step);
        field.Step(Step, karts);
        return field.Held(0) == PowerUpKind.None;
    }

    [Test]
    public void Bot_NewKinds_UsesThemByKartstersSimpleRules()
    {
        PowerUpKart[] far = [Kart(0f, 0f), Kart(5f, -100f)];
        PowerUpKart[] close = [Kart(0f, 0f), Kart(3f, -5f)];
        PowerUpKart[] ahead = [Kart(0f, 0f), Kart(0f, -10f)];
        PowerUpKart[] behind = [Kart(0f, 0f), Kart(0f, 10f)];
        PowerUpKart[] both = [Kart(0f, 0f), Kart(0f, -10f), Kart(0f, 10f)];

        Assert.That(BotUses(PowerUpKind.Invulnerable, far), Is.False, "no danger");
        Assert.That(BotUses(PowerUpKind.Invulnerable, close), Is.True, "a kart within 8 m");
        Assert.That(BotUses(PowerUpKind.Invulnerable, far, armedBananaAhead: true), Is.True, "a banana 20 m ahead");
        Assert.That(BotUses(PowerUpKind.Anvil, close), Is.True);
        Assert.That(BotUses(PowerUpKind.Anvil, far), Is.False);
        Assert.That(BotUses(PowerUpKind.Magnet, ahead), Is.True);
        Assert.That(BotUses(PowerUpKind.Magnet, both), Is.False, "would drag the one behind along");
        Assert.That(BotUses(PowerUpKind.Vampire, ahead), Is.True);
        Assert.That(BotUses(PowerUpKind.Vampire, behind), Is.False);
        Assert.That(BotUses(PowerUpKind.Locomotive, far), Is.True, "5-250 m ahead");
        Assert.That(BotUses(PowerUpKind.Locomotive, behind), Is.False);
        foreach (var dropped in (PowerUpKind[])[PowerUpKind.SmokeBomb, PowerUpKind.FakePowerUp, PowerUpKind.Roadblock,
                     PowerUpKind.ConcreteRoadblock])
        {
            Assert.That(BotUses(dropped, behind), Is.True, $"{dropped}: a kart 2-25 m behind");
            Assert.That(BotUses(dropped, ahead), Is.False, $"{dropped}: nobody behind");
        }

        foreach (var atOnce in (PowerUpKind[])[PowerUpKind.Lightning, PowerUpKind.Earthquake, PowerUpKind.Glitch,
                     PowerUpKind.FakeFinish])
            Assert.That(BotUses(atOnce, far), Is.True, $"{atOnce}: at once");
    }

    [Test]
    public void Step_EveryNewKindRunning_DoesNotAllocate()
    {
        var route = StarCircuitRoute.Value;
        var field = new PowerUpField([], 4, seed: 1, route);
        PowerUpKart[] karts = [OnRoad(route.Road, 300, 1), OnRoad(route.Road, 100, 2), OnRoad(route.Road, 60, 3),
            OnRoad(route.Road, 400, 4)];
        Use(field, karts, 0, PowerUpKind.Invulnerable);
        Use(field, karts, 1, PowerUpKind.Locomotive);
        Use(field, karts, 2, PowerUpKind.Magnet);
        Use(field, karts, 3, PowerUpKind.Lightning);
        Use(field, karts, 3, PowerUpKind.SmokeBomb);
        Use(field, karts, 2, PowerUpKind.Roadblock);
        Use(field, karts, 1, PowerUpKind.Vampire);
        Use(field, karts, 0, PowerUpKind.Glitch);
        Run(field, karts, 0.1f);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 240; step++)
        {
            field.Steer(1, 0.3f);
            field.ContactMass(0);
            field.SmokeCover(karts[0].Position);
            field.Step(Step, karts);
        }

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    //**********************************************************
    //** on the ECS karts
    //**********************************************************

    /// <summary>The kart model, the steering and contact systems, the physics and the power-ups.</summary>
    private static (SystemGroup Group, PowerUpField Field, Entity[] Karts) Karts(World world, params XnaVector2[] at)
    {
        var tunings = new KartTuning[at.Length];
        var karts = new Entity[at.Length];
        for (var i = 0; i < at.Length; i++)
        {
            tunings[i] = KartCatalog.Kart;
            karts[i] = KartEntity.Create(world, tunings[i], at[i], 0f, i);
        }

        var group = new SystemGroup(world);
        var physics = new KartPhysicsSystem(tunings);
        var field = new PowerUpField([], at.Length, seed: 1);
        physics.Effects = new KartEffectTuning(field);
        var steering = group.Add(new PowerUpSteeringSystem(field));
        group.Add(physics);
        group.Add(new PowerUpContactSystem(field, steering));
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        group.Add(new PowerUpSystem(field, physics));
        return (group, field, karts);
    }

    private static float Speed(World world, Entity kart) => world.Get<Velocity>(kart).Value.Length();

    /// <summary>Kart 1 rear-ends kart 0 at 15 m/s; kart 0's speed a second later.</summary>
    private static float SpeedAfterRearEnded(PowerUpKind? kind)
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, new XnaVector2(0f, 0f), new XnaVector2(0f, 6f));
        if (kind is { } held)
        {
            field.Give(0, held);
            field.RequestUse(0);
        }

        world.Get<Velocity>(karts[1]).Value = new XnaVector2(0f, -15f);
        for (var step = 0; step < 120; step++)
            group.Update(TrackScene.StepSeconds);
        return Speed(world, karts[0]);
    }

    [Test]
    public void Contact_InvulnerableOrAnvilKartRearEnded_IsHardlyOrLessShoved()
    {
        var plain = SpeedAfterRearEnded(null);
        var anvil = SpeedAfterRearEnded(PowerUpKind.Anvil);
        var invulnerable = SpeedAfterRearEnded(PowerUpKind.Invulnerable);

        TestContext.Out.WriteLine($"plain {plain:0.00}, anvil {anvil:0.00}, invulnerable {invulnerable:0.00} m/s");
        Assert.That(plain, Is.GreaterThan(3f));
        Assert.That(anvil, Is.LessThan(plain * 0.6f));
        Assert.That(invulnerable, Is.LessThan(0.5f));
    }

    [Test]
    public void Contact_InvulnerableKartAtFullThrottle_DrivesExactlyAsBefore()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, new XnaVector2(0f, 0f), new XnaVector2(40f, 0f));
        field.Give(0, PowerUpKind.Invulnerable);
        field.RequestUse(0);
        world.Get<KartControls>(karts[0]) = new KartControls { Throttle = 1f, Steer = 0.3f };
        world.Get<KartControls>(karts[1]) = new KartControls { Throttle = 1f, Steer = 0.3f };

        for (var step = 0; step < 240; step++)
            group.Update(TrackScene.StepSeconds);

        Assert.That(Speed(world, karts[0]), Is.EqualTo(Speed(world, karts[1])).Within(0.05f));
        Assert.That(world.Get<Rotation>(karts[0]).Radians, Is.EqualTo(world.Get<Rotation>(karts[1]).Radians)
            .Within(1e-3f));
        Assert.That(world.Get<RigidBody>(karts[0]).InverseMass, Is.EqualTo(1f / KartCatalog.Kart.Mass),
            "the real mass is back after the step");
    }

    [Test]
    public void Glitch_OnTheEcsKart_TurnsItTheOtherWayAndLeavesItsOwnSteering()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, new XnaVector2(0f, 0f), new XnaVector2(40f, 0f));
        field.Give(1, PowerUpKind.Glitch);
        field.RequestUse(1);
        group.Update(TrackScene.StepSeconds);
        world.Get<Velocity>(karts[0]).Value = new XnaVector2(0f, -10f);
        world.Get<KartControls>(karts[0]) = new KartControls { Throttle = 0.5f, Steer = 1f };

        for (var step = 0; step < 60; step++)
            group.Update(TrackScene.StepSeconds);

        Assert.That(KartFrame.ToHeading(world.Get<Rotation>(karts[0]).Radians), Is.GreaterThan(0.05f),
            "steering right turned it left");
        Assert.That(world.Get<KartControls>(karts[0]).Steer, Is.EqualTo(1f));
    }

    [Test]
    public void ConcreteRoadblock_OnTheEcsKarts_StandsAsAStaticBodyThatStopsAKart()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, new XnaVector2(0f, 0f), new XnaVector2(0f, -10f));
        field.Give(1, PowerUpKind.ConcreteRoadblock);
        field.RequestUse(1);
        var count = world.Count;
        group.Update(TrackScene.StepSeconds);
        Assert.That(world.Count, Is.EqualTo(count + 1), "one body");
        world.Get<Position>(karts[1]).Value = new XnaVector2(40f, 0f);
        world.Get<Velocity>(karts[0]).Value = new XnaVector2(0f, -15f);

        for (var step = 0; step < 120; step++)
            group.Update(TrackScene.StepSeconds);

        var z = world.Get<Position>(karts[0]).Value.Y;
        TestContext.Out.WriteLine($"stopped at z {z:0.00}, {Speed(world, karts[0]):0.00} m/s");
        Assert.That(Speed(world, karts[0]), Is.LessThan(1f));
        Assert.That(z, Is.GreaterThan(-6.5f + 0.3f + 1f), "in front of the block, not through it");
    }

    [Test]
    public void Roadblock_OnTheEcsKarts_IsShovedAsideByAKartThatKeepsGoing()
    {
        using var world = new World();
        var (group, field, karts) = Karts(world, new XnaVector2(0f, 0f), new XnaVector2(0f, -10f));
        field.Give(1, PowerUpKind.Roadblock);
        field.RequestUse(1);
        group.Update(TrackScene.StepSeconds);
        world.Get<Position>(karts[1]).Value = new XnaVector2(40f, 0f);
        world.Get<Velocity>(karts[0]).Value = new XnaVector2(0f, -20f);

        for (var step = 0; step < 60; step++)
            group.Update(TrackScene.StepSeconds);

        TestContext.Out.WriteLine($"kart {Speed(world, karts[0]):0.0} m/s, block at {field.Obstacles[0].Position}");
        Assert.That(Speed(world, karts[0]), Is.GreaterThan(8f));
        Assert.That(field.Obstacles[0].Position.Y, Is.LessThan(-8f), "knocked down the road");
    }
}
