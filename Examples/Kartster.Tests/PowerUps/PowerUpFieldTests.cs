using System;
using System.Numerics;
using Kartster.PowerUps;
using NUnit.Framework;

namespace Kartster.Tests.PowerUps;

/// <summary>The pure power-up rules: pads, the reel, the draw, effects, bananas and cans.</summary>
public class PowerUpFieldTests
{
    private const float Step = 1f / 120f;

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

    /// <summary>Moves each kart by its velocity and steps the field, <paramref name="seconds"/> long.</summary>
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

    [Test]
    public void Step_KartDrivesThroughPad_TakesItAndPadGoesDormant()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, -10f)], 1, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f, speed: 20f)];

        Run(field, karts, 1f);

        Assert.That(field.IsSpinning(0), Is.True, "the reel spins");
        Assert.That(field.IsHolding(0), Is.True);
        Assert.That(field.Pads[0].IsAvailable, Is.False);
        Assert.That(field.Pads[0].RespawnSeconds, Is.GreaterThan(4f).And.LessThanOrEqualTo(5f));
    }

    [Test]
    public void Step_KartMissesPadByMoreThanThePickupRadius_TakesNothing()
    {
        var field = new PowerUpField([new Vector3(0.8f + 1.25f, 1f, -10f)], 1, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f, speed: 20f)];

        Run(field, karts, 1f);

        Assert.That(field.IsHolding(0), Is.False);
        Assert.That(field.Pads[0].IsAvailable, Is.True);
    }

    [Test]
    public void Step_RowOfPadsWithSlotFilledByTheFirst_LeavesTheRestForTheNextKart()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, -20f)], 1, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f, speed: 20f)];

        Run(field, karts, 1.5f);

        Assert.That(field.Pads[0].IsAvailable, Is.False);
        Assert.That(field.Pads[1].IsAvailable, Is.True, "a kart that holds something leaves the pad");
    }

    [Test]
    public void Step_HoldingAPowerUp_DoesNotTakeAPad()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, 0f)], 1, seed: 1);
        field.Give(0, PowerUpKind.Banana);
        PowerUpKart[] karts = [Kart(0f, 0f)];

        Run(field, karts, 0.5f);

        Assert.That(field.Pads[0].IsAvailable, Is.True);
        Assert.That(field.Held(0), Is.EqualTo(PowerUpKind.Banana));
        Assert.That(field.Charges(0), Is.EqualTo(5));
    }

    [Test]
    public void Step_TakenPad_IsBackAfterFiveSeconds()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, 0f)], 1, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f)];
        field.Step(Step, karts);
        Assert.That(field.Pads[0].IsAvailable, Is.False);
        karts[0].Position = new Vector2(100f, 0f); // away, so it can't take the pad again

        Run(field, karts, PowerUpCatalog.RespawnSeconds - 0.1f);
        Assert.That(field.Pads[0].IsAvailable, Is.False);
        Run(field, karts, 0.2f);
        Assert.That(field.Pads[0].IsAvailable, Is.True);
    }

    [Test]
    public void Step_ReelSpinsTwoAndAHalfSeconds_ThenHoldsWhatItShows()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, 0f)], 1, seed: 7);
        PowerUpKart[] karts = [Kart(0f, 0f)];
        field.Step(Step, karts);

        Run(field, karts, PowerUpCatalog.SpinSeconds - 0.1f);
        Assert.That(field.IsSpinning(0), Is.True);
        Assert.That(field.Held(0), Is.EqualTo(PowerUpKind.None), "nothing can be used while it spins");
        var showing = field.Showing(0);
        Run(field, karts, 0.2f);

        Assert.That(field.IsSpinning(0), Is.False);
        Assert.That(field.Held(0), Is.EqualTo(showing));
        Assert.That(field.Charges(0), Is.EqualTo(PowerUpCatalog.Charges(showing)));
    }

    [Test]
    public void RequestUse_WhileTheReelSpins_StopsItOnItsFaceWithoutFiring()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, 0f)], 1, seed: 7);
        PowerUpKart[] karts = [Kart(0f, 0f)];
        field.Step(Step, karts);
        Run(field, karts, 0.5f);
        var showing = field.Showing(0);

        field.RequestUse(0);
        field.Step(Step, karts);

        Assert.That(field.IsSpinning(0), Is.False);
        Assert.That(field.Held(0), Is.EqualTo(showing));
        Assert.That(field.Charges(0), Is.EqualTo(PowerUpCatalog.Charges(showing)), "the press was spent choosing");
    }

    [Test]
    public void Draw_SameSeed_GivesTheSameReel()
    {
        PowerUpKind[] Reel(int seed)
        {
            var field = new PowerUpField([new Vector3(0f, 1f, 0f)], 1, seed);
            PowerUpKart[] karts = [Kart(0f, 0f)];
            var faces = new PowerUpKind[40];
            for (var i = 0; i < faces.Length; i++)
            {
                field.Step(0.05f, karts);
                faces[i] = field.Showing(0);
            }

            return faces;
        }

        Assert.That(Reel(42), Is.EqualTo(Reel(42)));
        Assert.That(Reel(42), Is.Not.EqualTo(Reel(43)));
    }

    [Test]
    public void Roll_ByRacePosition_LeaderGetsNoCansAndTheLastKartGetsMoreBoosts()
    {
        var random = new Random(3);
        const int draws = 20000;
        int leaderCans = 0, leaderBoosts = 0, lastCans = 0, lastBoosts = 0;
        for (var i = 0; i < draws; i++)
        {
            var leader = PowerUpCatalog.Roll(0f, random);
            var last = PowerUpCatalog.Roll(1f, random);
            if (PowerUpCatalog.IsCan(leader)) leaderCans++;
            if (leader == PowerUpKind.SpeedBoost) leaderBoosts++;
            if (PowerUpCatalog.IsCan(last)) lastCans++;
            if (last == PowerUpKind.SpeedBoost) lastBoosts++;
        }

        // Kartster's weights: speed boost 2 → 6, bananas 4 → 1, cans 0 → 6.3 (of 10 at the front, 19.3 at the back).
        Assert.That(leaderCans, Is.Zero);
        Assert.That(leaderBoosts / (float)draws, Is.EqualTo(0.2f).Within(0.015f));
        Assert.That(lastBoosts / (float)draws, Is.EqualTo(6f / 19.3f).Within(0.015f));
        Assert.That(lastCans / (float)draws, Is.EqualTo(6.3f / 19.3f).Within(0.015f));
        Assert.That(PowerUpCatalog.WeightAt(PowerUpKind.Banana, 0.5f), Is.EqualTo(2.5f));
    }

    [Test]
    public void SpeedBoost_Used_AddsNineMetresPerSecondAndPowerAndGripForTenSeconds()
    {
        var field = new PowerUpField([], 1, seed: 1);
        field.Give(0, PowerUpKind.SpeedBoost);
        PowerUpKart[] karts = [Kart(0f, 0f, speed: 10f)];

        field.RequestUse(0);
        field.Step(Step, karts);

        Assert.That(Vector2.Dot(karts[0].Velocity, karts[0].Forward), Is.EqualTo(19f).Within(1e-4f));
        Assert.That(field.Held(0), Is.EqualTo(PowerUpKind.None), "one charge");
        var effects = field.Effects(0);
        Assert.That(effects.EnginePower, Is.EqualTo(1.6f));
        Assert.That(effects.TyreGrip, Is.EqualTo(1.2f));
        Assert.That(effects.IsBoosting, Is.True);
        Run(field, karts, 9.9f);
        Assert.That(effects.EnginePower, Is.EqualTo(1.6f), "a boost does not fade");
        Run(field, karts, 0.2f);
        Assert.That(effects.EnginePower, Is.EqualTo(1f));
        Assert.That(effects.IsBoosting, Is.False);
    }

    [Test]
    public void Effects_SamePowerUpTwice_RefreshesAndDifferentOnesMultiply()
    {
        var effects = new KartEffects();
        effects.Add(PowerUpKind.EngineTune, EffectChannel.EnginePower, 1.35f, 10f);
        effects.Add(PowerUpKind.EngineTune, EffectChannel.EnginePower, 1.35f, 10f);
        Assert.That(effects.EnginePower, Is.EqualTo(1.35f));

        effects.Add(PowerUpKind.SpeedBoost, EffectChannel.EnginePower, 1.6f, 10f);
        Assert.That(effects.EnginePower, Is.EqualTo(1.35f * 1.6f).Within(1e-5f));
    }

    [Test]
    public void Use_CountdownRunning_DoesNothing()
    {
        var field = new PowerUpField([], 1, seed: 1) { CanUse = false };
        field.Give(0, PowerUpKind.SpeedBoost);
        PowerUpKart[] karts = [Kart(0f, 0f)];

        field.RequestUse(0);
        field.Step(Step, karts);

        Assert.That(field.Held(0), Is.EqualTo(PowerUpKind.SpeedBoost));
        Assert.That(karts[0].Velocity, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void Banana_KartDrivesIntoIt_SpinsAndLosesMostGripFadingBack()
    {
        var field = new PowerUpField([], 2, seed: 1);
        field.Give(0, PowerUpKind.Banana);
        // The dropper faces +Z (heading π), so the banana lands 2.5 m behind it, towards -Z.
        PowerUpKart[] karts = [Kart(0f, 0f, heading: MathF.PI), Kart(0f, -30f)];
        field.RequestUse(0);
        field.Step(Step, karts);
        Assert.That(field.Hazards.Length, Is.EqualTo(1));
        Assert.That(field.Hazards[0].Position.Y, Is.EqualTo(-2.5f).Within(1e-4f));
        Assert.That(field.Charges(0), Is.EqualTo(4));
        karts[0].Position = new Vector2(50f, 0f);

        karts[1].Velocity = new Vector2(0f, 15f); // drives at it, the kart frame's +Z
        Run(field, karts, 2f);

        Assert.That(field.Hazards.Length, Is.Zero, "used up");
        Assert.That(MathF.Abs(karts[1].YawRate), Is.EqualTo(PowerUpCatalog.BananaSpin));
        Assert.That(karts[1].Velocity.Length(), Is.EqualTo(15f).Within(1e-3f), "a banana keeps the speed");
        var grip = field.Effects(1).TyreGrip;
        Assert.That(grip, Is.GreaterThan(PowerUpCatalog.BananaGrip).And.LessThan(1f), "fading back");
    }

    [Test]
    public void Banana_TheDropperStillOnIt_OnlyHitsOnceArmed()
    {
        var field = new PowerUpField([], 1, seed: 1);
        field.Give(0, PowerUpKind.Banana);
        PowerUpKart[] karts = [Kart(0f, -2.5f)]; // facing -Z, the banana lands right under the next spot
        field.RequestUse(0);
        field.Step(Step, karts);
        karts[0].Position = field.Hazards[0].Position;

        Run(field, karts, PowerUpCatalog.BananaArmingSeconds - 0.05f);
        Assert.That(field.Hazards.Length, Is.EqualTo(1), "not armed yet");
        Run(field, karts, 0.1f);
        Assert.That(field.Hazards.Length, Is.Zero, "nobody is exempt from their own banana");
    }

    [Test]
    public void Banana_ThrownAheadFromAStandstill_LandsAbout47MetresAhead()
    {
        var field = new PowerUpField([], 1, seed: 1);
        field.Give(0, PowerUpKind.Banana);
        PowerUpKart[] karts = [Kart(0f, 0f)];
        field.RequestUse(0, PowerUpAim.Forward);

        Run(field, karts, 3f);

        Assert.That(field.Hazards[0].InAir, Is.False);
        // 1.5 m ahead, 1.4 m up, 25 m/s ahead and 8.5 m/s up: about 1.88 s in the air (Kartster: "some 47 m").
        Assert.That(-field.Hazards[0].Position.Y, Is.EqualTo(48.6f).Within(0.5f));
    }

    [Test]
    public void Lapskaus_FiredAtAKartAhead_HitsItSpinsItAndSkidsItAside()
    {
        var field = new PowerUpField([], 2, seed: 1);
        field.Give(0, PowerUpKind.Lapskaus);
        PowerUpKart[] karts = [Kart(0f, 0f, position: 2), Kart(1f, -40f, speed: 10f, position: 1)];
        field.RequestUse(0);

        field.Step(Step, karts);
        Assert.That(field.Projectiles.Length, Is.EqualTo(1));
        for (var step = 0; step < 240 && field.Projectiles.Length > 0; step++)
            field.Step(Step, karts);

        Assert.That(field.Projectiles.Length, Is.Zero);
        var victim = karts[1];
        Assert.That(Vector2.Dot(victim.Velocity, victim.Forward), Is.EqualTo(4f).Within(1e-3f), "keeps 40%");
        Assert.That(victim.Velocity.X, Is.EqualTo(PowerUpCatalog.CanSkid).Within(1e-3f), "away from the shooter");
        Assert.That(MathF.Abs(victim.YawRate), Is.EqualTo(PowerUpCatalog.CanSpin));
        Assert.That(field.Effects(1).TyreGrip, Is.LessThan(0.3f));
    }

    [Test]
    public void Lapskaus_DroppedBehind_StopsTheKartThatDrivesIntoIt()
    {
        var field = new PowerUpField([], 2, seed: 1);
        field.Give(0, PowerUpKind.Lapskaus);
        PowerUpKart[] karts = [Kart(0f, 0f, heading: MathF.PI), Kart(0f, -20f)];
        field.RequestUse(0, PowerUpAim.Backward);
        field.Step(Step, karts);
        Assert.That(field.Projectiles.Length, Is.Zero);
        Assert.That(field.Hazards.Length, Is.EqualTo(1));
        karts[0].Position = new Vector2(50f, 0f);

        karts[1].Velocity = new Vector2(0f, 15f);
        Run(field, karts, 2f);

        Assert.That(field.Hazards.Length, Is.Zero);
        Assert.That(karts[1].Velocity, Is.EqualTo(Vector2.Zero), "stopped dead");
    }

    [Test]
    public void HomingJoika_KartAheadOffToTheSide_TurnsAndHitsIt()
    {
        var field = new PowerUpField([], 3, seed: 1);
        field.Give(0, PowerUpKind.HomingJoika);
        // The kart one place ahead is 15 m to the side; the leader is straight ahead.
        PowerUpKart[] karts = [Kart(0f, 0f, position: 3), Kart(15f, -30f, position: 2), Kart(0f, -60f, position: 1)];
        field.RequestUse(0);

        for (var step = 0; step < 4 * 120; step++)
            field.Step(Step, karts);

        Assert.That(MathF.Abs(karts[1].YawRate), Is.EqualTo(PowerUpCatalog.CanSpin), "the kart one place ahead");
        Assert.That(karts[2].YawRate, Is.Zero);
    }

    [Test]
    public void TurnToward_LimitsTheTurnAndKeepsTheSpeed()
    {
        var turned = PowerUpField.TurnToward(new Vector2(10f, 0f), new Vector2(0f, 1f), 0.1f);

        Assert.That(turned.Length(), Is.EqualTo(10f).Within(1e-4f));
        Assert.That(MathF.Atan2(turned.Y, turned.X), Is.EqualTo(0.1f).Within(1e-5f));
    }

    [Test]
    public void Step_ManyStepsWithEverythingOnTheRoad_DoesNotAllocate()
    {
        var field = new PowerUpField([new Vector3(0f, 1f, -10f), new Vector3(30f, 1f, 0f)], 3, seed: 1);
        PowerUpKart[] karts = [Kart(0f, 0f, speed: 5f, position: 3), Kart(5f, -40f, position: 2), Kart(0f, -80f)];
        field.Give(0, PowerUpKind.TripleHomingJoika);
        field.Give(1, PowerUpKind.Banana);
        field.Give(2, PowerUpKind.SpeedBoost);
        field.RequestUse(0);
        field.RequestUse(1);
        field.RequestUse(2);
        field.Step(Step, karts);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 240; step++)
        {
            if (step % 30 == 0)
                field.RequestUse(0);
            field.Step(Step, karts);
        }

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
