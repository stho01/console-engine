using System;
using System.Collections.Generic;
using System.Numerics;
using KartViewer.Driving;
using NUnit.Framework;

namespace KartViewer.Tests.Driving;

/// <summary>
/// Drives <see cref="KartDynamics"/> through the manoeuvres Kartster's own physics was measured with and compares the
/// numbers (plan § 7). The reference values come from Kartster's <c>ArcadeKartPhysics.Update</c> on flat ground
/// (<c>DriftProbe.Step</c> grounding, 120 Hz, step inputs, Joyride, SemiAutomatic gearbox), measured on 2026-11-14 with
/// a scratch harness against the unmodified kartster-server source; method and raw output in the step-4 report
/// (<c>kart-reference.md</c>, summarised in <c>docs/design/kart-physics-plan.md</c> § 7).
/// </summary>
public class KartReferenceTests
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float Dt = 1f / 120;
    private const int StepsPerSecond = 120;

    // The harness's targets and its proportional speed controller (full pedal at 1/Gain m/s of error).
    private const float Speed50 = 13.889f;
    private const float Speed80 = 22.222f;
    private const float StopSpeed = 0.5f;
    private const float TurnControllerGain = 3f;
    private const int TurnSteps = 5 * StepsPerSecond;
    private const int HandbrakeSteps = StepsPerSecond;

    /// <summary>Kartster's numbers. Speeds in m/s, times in s, distances in m, heading change in degrees.</summary>
    private sealed record Reference(float TopSpeed, float ZeroTo50, float ZeroTo80, float BrakingDistance,
        float TurnRadius20, float TurnRadius50, float GrassTopSpeed, float HandbrakeHeadingChange);

    private static readonly Dictionary<string, Reference> Kartster = new()
    {
        ["Kart"] = new Reference(TopSpeed: 32.001f, ZeroTo50: 0.658f, ZeroTo80: 1.675f, BrakingDistance: 8.844f,
            TurnRadius20: 2.865f, TurnRadius50: 14.819f, GrassTopSpeed: 16.000f, HandbrakeHeadingChange: -86.21f),
        // Kartster's olabil also rolls its body (KartBodyRoll), which we leave out: about 3 % tighter turns (plan § 10).
        ["olabil"] = new Reference(TopSpeed: 32.001f, ZeroTo50: 0.658f, ZeroTo80: 1.675f, BrakingDistance: 8.844f,
            TurnRadius20: 2.973f, TurnRadius50: 17.519f, GrassTopSpeed: 16.000f, HandbrakeHeadingChange: -77.46f),
        ["wagon"] = new Reference(TopSpeed: 32.214f, ZeroTo50: 0.683f, ZeroTo80: 1.867f, BrakingDistance: 7.160f,
            TurnRadius20: 2.839f, TurnRadius50: 12.789f, GrassTopSpeed: 16.107f, HandbrakeHeadingChange: -101.99f),
    };

    private static readonly string[] Karts = ["Kart", "olabil", "wagon"];
    private static readonly KartControls FullThrottle = new() { Throttle = 1f };

    //**********************************************************
    //** tests
    //**********************************************************

    [TestCaseSource(nameof(Karts))]
    public void TopSpeed_FullThrottleFor60Seconds_MatchesKartster(string name)
    {
        var kart = Start(name, out var body);
        Run(kart, ref body, FullThrottle, 60 * StepsPerSecond);

        Assert.That(Speed(body), Is.EqualTo(Kartster[name].TopSpeed).Within(3).Percent);
    }

    [TestCaseSource(nameof(Karts))]
    public void Acceleration_FullThrottleFromStandstill_ReachesSpeedsAsKartster(string name)
    {
        var kart = Start(name, out var body);
        var zeroTo50 = float.NaN;
        var zeroTo80 = float.NaN;
        for (var i = 1; i <= 60 * StepsPerSecond && float.IsNaN(zeroTo80); i++)
        {
            Step(kart, ref body, FullThrottle, Surface.Asphalt);
            if (float.IsNaN(zeroTo50) && Speed(body) >= Speed50)
                zeroTo50 = i * Dt;
            if (Speed(body) >= Speed80)
                zeroTo80 = i * Dt;
        }

        Assert.That(zeroTo50, Is.EqualTo(Kartster[name].ZeroTo50).Within(10).Percent);
        Assert.That(zeroTo80, Is.EqualTo(Kartster[name].ZeroTo80).Within(10).Percent);
    }

    [TestCaseSource(nameof(Karts))]
    public void BrakingDistance_FullBrakeFrom80KilometresPerHour_MatchesKartster(string name)
    {
        var kart = Start(name, out var body);
        DriveTo(kart, ref body, Speed80);
        var start = body.Position;
        for (var i = 0; Speed(body) >= StopSpeed && i < 30 * StepsPerSecond; i++)
            Step(kart, ref body, new KartControls { Brake = 1f }, Surface.Asphalt);

        Assert.That(Vector2.Distance(start, body.Position), Is.EqualTo(Kartster[name].BrakingDistance).Within(10).Percent);
    }

    [TestCase("Kart", 20f)]
    [TestCase("Kart", 50f)]
    [TestCase("olabil", 20f)]
    [TestCase("olabil", 50f)]
    [TestCase("wagon", 20f)]
    [TestCase("wagon", 50f)]
    public void TurnRadius_FullRightLockHeldAtSpeedFor5Seconds_MatchesKartster(string name, float kilometresPerHour)
    {
        var target = kilometresPerHour / 3.6f;
        var kart = Start(name, out var body);
        DriveTo(kart, ref body, target);
        for (var i = 0; i < TurnSteps; i++)
        {
            var pedal = TurnControllerGain * (target - Speed(body));
            var controls = new KartControls { Throttle = Pedal(pedal), Brake = Pedal(-pedal), Steer = 1f };
            Step(kart, ref body, controls, Surface.Asphalt);
        }

        var radius = Speed(body) / MathF.Abs(body.AngularVelocity);
        var reference = kilometresPerHour < 30f ? Kartster[name].TurnRadius20 : Kartster[name].TurnRadius50;
        Assert.That(body.AngularVelocity, Is.Negative); // right
        Assert.That(radius, Is.EqualTo(reference).Within(10).Percent);
    }

    [TestCaseSource(nameof(Karts))]
    public void TopSpeed_FullThrottleOnGrassFor60Seconds_MatchesKartster(string name)
    {
        var kart = Start(name, out var body);
        Run(kart, ref body, FullThrottle, 60 * StepsPerSecond, Surface.Grass);

        Assert.That(Speed(body), Is.EqualTo(Kartster[name].GrassTopSpeed).Within(5).Percent);
    }

    [TestCaseSource(nameof(Karts))]
    public void HeadingChange_HandbrakeAndFullRightLockFor1SecondFrom50KilometresPerHour_MatchesKartster(string name)
    {
        var kart = Start(name, out var body);
        DriveTo(kart, ref body, Speed50);
        var startHeading = body.Heading;
        Run(kart, ref body, new KartControls { Steer = 1f, Handbrake = true }, HandbrakeSteps);

        var headingChange = float.RadiansToDegrees(body.Heading - startHeading);
        Assert.That(headingChange, Is.EqualTo(Kartster[name].HandbrakeHeadingChange).Within(15).Percent);
    }

    // Kartster, same harness: full throttle to 31.5 m/s, then full right lock through the keyboard ramp (0.25 s out)
    // with the throttle held for 3 s. Kartster neither drifts here (body slip under 2.5°) nor should we.
    [TestCase("Kart", -83.3f)]
    [TestCase("olabil", -75.1f)]
    [TestCase("wagon", -95.2f)]
    public void HeadingChange_FullRightLockFor3SecondsAtTopSpeed_MatchesKartster(string name, float kartsterDegrees)
    {
        var kart = Start(name, out var body);
        DriveTo(kart, ref body, 31.5f);
        var startHeading = body.Heading;
        var maxSlipDegrees = 0f;
        var steer = 0f;
        for (var i = 0; i < 3 * StepsPerSecond; i++)
        {
            steer = MathF.Min(steer + Dt / 0.25f, 1f);
            Step(kart, ref body, new KartControls { Throttle = 1f, Steer = steer }, Surface.Asphalt);
            var slip = MathF.Atan2(Vector2.Dot(body.Velocity, body.Right), Vector2.Dot(body.Velocity, body.Forward));
            maxSlipDegrees = MathF.Max(maxSlipDegrees, MathF.Abs(float.RadiansToDegrees(slip)));
        }

        var headingChange = float.RadiansToDegrees(body.Heading - startHeading);
        Assert.That(headingChange, Is.EqualTo(kartsterDegrees).Within(10).Percent);
        Assert.That(maxSlipDegrees, Is.LessThan(3f));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static KartDynamics Start(string name, out KartBody body)
    {
        body = default;
        return new KartDynamics(KartCatalog.ForKart(name));
    }

    private static void Step(KartDynamics kart, ref KartBody body, KartControls controls, Surface surface)
    {
        var forces = kart.ComputeForces(body, controls, surface, Dt);
        KartIntegrator.Step(ref body, forces, kart.Mass, kart.YawInertia, Dt);
    }

    private static void Run(KartDynamics kart, ref KartBody body, KartControls controls, int steps,
        Surface surface = Surface.Asphalt)
    {
        for (var i = 0; i < steps; i++)
            Step(kart, ref body, controls, surface);
    }

    // Full throttle straight ahead until the speed reaches the target, as the harness's DriveTo.
    private static void DriveTo(KartDynamics kart, ref KartBody body, float target)
    {
        for (var i = 0; i < 60 * StepsPerSecond && Speed(body) < target; i++)
            Step(kart, ref body, FullThrottle, Surface.Asphalt);
    }

    // Kartster's pedals are 16-bit (KartInputState.Throttle/Brake), and the controller's output is rounded to them.
    private static float Pedal(float value) => MathF.Round(Math.Clamp(value, 0f, 1f) * ushort.MaxValue) / ushort.MaxValue;

    private static float Speed(KartBody body) => body.Velocity.Length();
}
