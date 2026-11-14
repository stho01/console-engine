using System;
using System.Numerics;
using KartViewer.Driving;
using NUnit.Framework;

namespace KartViewer.Tests.Driving;

public class KartDynamicsTests
{
    private const float Dt = 1f / 120;
    private const int StepsPerSecond = 120;
    private const float KilometresPerHour = 1f / 3.6f;

    private static readonly KartControls FullThrottle = new() { Throttle = 1f };

    private static KartTuning Kart(string name) => KartCatalog.ForKart(name);

    private static KartBody Moving(float forwardSpeed) => new() { Velocity = new Vector2(0f, -forwardSpeed) };

    private static KartBody Drive(KartDynamics kart, KartBody body, KartControls controls, float seconds,
        Surface surface = Surface.Asphalt)
    {
        for (var i = 0; i < seconds * StepsPerSecond; i++)
            Step(kart, ref body, controls, surface);
        return body;
    }

    private static void Step(KartDynamics kart, ref KartBody body, KartControls controls,
        Surface surface = Surface.Asphalt)
    {
        var forces = kart.ComputeForces(body, controls, surface, Dt);
        KartIntegrator.Step(ref body, forces, kart.Mass, kart.YawInertia, Dt);
    }

    private static float ForwardSpeed(KartBody body) => Vector2.Dot(body.Velocity, body.Forward);

    [TestCase("Kart")]
    [TestCase("olabil")]
    [TestCase("wagon")]
    public void ComputeForces_FullThrottleOnAsphalt_SettlesAtPowerBalanceTopSpeed(string name)
    {
        var kart = new KartDynamics(Kart(name));

        var body = Drive(kart, default, FullThrottle, seconds: 60);

        var expected = KartEngine.GetTopSpeed(kart.Tuning);
        Assert.That(ForwardSpeed(body), Is.EqualTo(expected).Within(2).Percent);
        Assert.That(body.Position.Y, Is.LessThan(-1000f)); // forward is -Z
        Assert.That(MathF.Abs(body.Position.X), Is.LessThan(0.01f));
    }

    [Test]
    public void TopSpeed_DefaultKartInJoyride_IsAbout32MetresPerSecond()
    {
        Assert.That(new KartDynamics(KartCatalog.Kart).TopSpeed, Is.EqualTo(32f).Within(0.5f));
    }

    [TestCase("Kart")]
    [TestCase("wagon")]
    public void ComputeForces_FullThrottleOnGrass_SettlesAtHalfTheAsphaltTopSpeed(string name)
    {
        var kart = new KartDynamics(Kart(name));

        var body = Drive(kart, default, FullThrottle, seconds: 30, Surface.Grass);

        Assert.That(ForwardSpeed(body), Is.EqualTo(kart.TopSpeed * 0.5f).Within(2).Percent);
    }

    [TestCase("Kart")]
    [TestCase("olabil")]
    [TestCase("wagon")]
    public void ComputeForces_BrakeFrom80KilometresPerHour_StopsWithinThreeSeconds(string name)
    {
        var kart = new KartDynamics(Kart(name));
        var body = Moving(80f * KilometresPerHour);
        var brake = new KartControls { Brake = 1f };

        var steps = 0;
        while (ForwardSpeed(body) > 0.1f && steps < 10 * StepsPerSecond)
        {
            Step(kart, ref body, brake);
            steps++;
        }

        Assert.That(steps, Is.LessThan(3 * StepsPerSecond));
        Assert.That(body.Heading, Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void ComputeForces_BrakeHeldFromStandstill_ReversesAtMostAt30KilometresPerHour()
    {
        var kart = new KartDynamics(KartCatalog.Kart);

        var body = Drive(kart, default, new KartControls { Brake = 1f }, seconds: 10);

        Assert.That(ForwardSpeed(body), Is.EqualTo(-KartDynamics.MaximumReverseSpeed).Within(0.05f));
        Assert.That(body.Position.Y, Is.GreaterThan(0f));
    }

    [Test]
    public void ComputeForces_SteerRightAtSpeed_TurnsRight()
    {
        var kart = new KartDynamics(KartCatalog.Kart);

        var body = Drive(kart, Moving(15f), new KartControls { Throttle = 1f, Steer = 1f }, seconds: 1);

        Assert.That(body.Heading, Is.LessThan(-0.3f));
        Assert.That(body.Position.X, Is.GreaterThan(0f)); // right of -Z is +X
    }

    [Test]
    public void ComputeForces_SteerWhileStandingStill_DoesNotTurn()
    {
        var kart = new KartDynamics(KartCatalog.Kart);

        var body = Drive(kart, default, new KartControls { Steer = 1f }, seconds: 1);

        Assert.That(body.Heading, Is.Zero);
        Assert.That(body.Velocity, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void ComputeForces_HandbrakeInACorner_SlidesAndYawsMoreThanWithout()
    {
        var steer = new KartControls { Throttle = 1f, Steer = 1f };
        var handbrake = steer with { Handbrake = true };
        var gripping = new KartDynamics(KartCatalog.Kart);
        var drifting = new KartDynamics(KartCatalog.Kart);

        var grippingBody = Drive(gripping, Moving(20f), steer, seconds: 0.5f);
        var driftingBody = Moving(20f);
        var slid = false;
        for (var i = 0; i < StepsPerSecond / 2; i++)
        {
            Step(drifting, ref driftingBody, handbrake);
            slid |= drifting.IsSliding;
        }

        Assert.That(slid, Is.True);
        Assert.That(-driftingBody.Heading, Is.GreaterThan(-grippingBody.Heading));
    }

    [Test]
    public void ComputeForces_WagonAtFullThrottle_ShiftsUpThroughTheGears()
    {
        var kart = new KartDynamics(KartCatalog.Wagon);
        var body = default(KartBody);
        var highestGear = 1;

        for (var i = 0; i < 30 * StepsPerSecond; i++)
        {
            Step(kart, ref body, FullThrottle);
            Assert.That(kart.Gear, Is.GreaterThanOrEqualTo(highestGear)); // never down on a straight
            highestGear = kart.Gear;
        }

        Assert.That(highestGear, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void ComputeForces_InSteadyState_DoesNotAllocate()
    {
        var kart = new KartDynamics(KartCatalog.Wagon);
        var body = Drive(kart, default, FullThrottle, seconds: 1);
        var controls = new KartControls { Throttle = 1f, Steer = 0.5f, Handbrake = true };

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            Step(kart, ref body, controls, (Surface)(i % 4));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    [TestCase("Kart", "Kart")]
    [TestCase("WAGON", "wagon")]
    [TestCase("olabil", "olabil")]
    [TestCase("unknown", "Kart")]
    public void ForKart_FolderName_ReturnsThatKartOrTheDefault(string folder, string expected)
    {
        var tunings = new[] { KartCatalog.Kart, KartCatalog.Olabil, KartCatalog.Wagon };
        var names = new[] { "Kart", "olabil", "wagon" };

        Assert.That(KartCatalog.ForKart(folder), Is.SameAs(tunings[Array.IndexOf(names, expected)]));
    }

    [Test]
    public void YawInertia_DefaultKart_IsSolidBoxOfTheChassis()
    {
        var kart = new KartDynamics(KartCatalog.Kart);

        Assert.That(kart.YawInertia, Is.EqualTo(200f * (1.6f * 1.6f + 2.8f * 2.8f) / 12f).Within(1e-3f));
    }
}
