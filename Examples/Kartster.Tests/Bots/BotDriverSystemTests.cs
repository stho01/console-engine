using System;
using System.IO;
using System.Numerics;
using Kartster.Bots;
using Kartster.Components;
using Kartster.Driving;
using NUnit.Framework;

namespace Kartster.Tests.Bots;

public class BotDriverSystemTests
{
    private const float Step = 1f / 120f;

    private static readonly Lazy<Track> StarCircuit =
        new(() => Track.Load(Path.Combine("Assets", "Tracks"), "star-circuit"));

    private static readonly KartTuning Tuning = KartCatalog.Kart;

    private static BotRoutePlan Plan() => new(StarCircuit.Value, Tuning, BotDriverSystem.CornerSpeedFactor(0.5f),
        BotDriverSystem.BrakingShare(0.5f));

    /// <summary>A kart at <paramref name="sample"/>'s centerline point, turned <paramref name="turnedLeft"/> radians
    /// left of the route, going <paramref name="speed"/> m/s forwards.</summary>
    private static KartBody KartAt(BotRoutePlan plan, int sample, float turnedLeft, float speed)
    {
        var point = plan.PointAhead(sample, 0f, 0f);
        var ahead = plan.PointAhead(sample, 2f, 0f) - point;
        var body = new KartBody
        {
            Position = point,
            Heading = MathF.Atan2(-ahead.X, -ahead.Y) + turnedLeft, // as KartFrame.HeadingOf
        };
        body.Velocity = body.Forward * speed;
        return body;
    }

    /// <summary>Steers for a few steps (the steering ramps) and returns the last controls.</summary>
    private static KartControls Drive(BotRoutePlan plan, KartBody body, float laneOffset = 0f, int steps = 30)
    {
        var bot = new Bot { Skill = 0.5f, LaneOffset = laneOffset, Sample = -1 };
        var controls = new KartControls();
        for (var i = 0; i < steps; i++)
            BotDriverSystem.Drive(ref bot, ref controls, body, Surface.Asphalt, Tuning, plan, Step);
        return controls;
    }

    private static int SharpestCorner(BotRoutePlan plan)
    {
        var slowest = 0;
        for (var i = 0; i < plan.Count; i++)
            if (plan.SpeedAhead(i, 0f) < plan.SpeedAhead(slowest, 0f))
                slowest = i;
        return slowest;
    }

    [TestCase(0.3f, ExpectedResult = 1)]   // turned left of the route: steer right
    [TestCase(-0.3f, ExpectedResult = -1)] // turned right: steer left
    public int Drive_KartTurnedOffTheRoute_SteersTowardsTheLookahead(float turnedLeft)
    {
        var plan = Plan();
        var controls = Drive(plan, KartAt(plan, 10, turnedLeft, 10f));

        Assert.That(MathF.Abs(controls.Steer), Is.GreaterThan(0.2f));
        return Math.Sign(controls.Steer);
    }

    [Test]
    public void Drive_LaneRightOfTheKart_SteersRight()
    {
        var plan = Plan();
        var body = KartAt(plan, 10, 0f, 10f);

        var onLine = Drive(plan, body);
        var laneRight = Drive(plan, body, laneOffset: 2f);

        Assert.That(laneRight.Steer, Is.GreaterThan(onLine.Steer + 0.05f));
    }

    [Test]
    public void Drive_FastBeforeTheSharpestCorner_Brakes()
    {
        var plan = Plan();
        var corner = SharpestCorner(plan);
        var before = corner - (int)(15f / plan.Spacing);
        var cornerSpeed = plan.SpeedAhead(corner, 0f);

        var controls = Drive(plan, KartAt(plan, before, 0f, cornerSpeed + 12f), steps: 1);

        TestContext.Out.WriteLine($"corner at {corner * plan.Spacing:0} m, {cornerSpeed * 3.6f:0} km/h");
        Assert.That(cornerSpeed, Is.LessThan(KartEngine.GetTopSpeed(Tuning) * 0.7f), "a real corner");
        Assert.That(controls.Brake, Is.GreaterThan(0.5f));
        Assert.That(controls.Throttle, Is.Zero);
    }

    [Test]
    public void Drive_SlowOnTheGrid_GivesThrottle()
    {
        var plan = Plan();

        var controls = Drive(plan, KartAt(plan, 0, 0f, 0f), steps: 1);

        Assert.That(controls.Throttle, Is.GreaterThan(0.8f));
        Assert.That(controls.Brake, Is.Zero);
    }

    [Test]
    public void Drive_ThrottleButNotMoving_ReversesAfterOneAndAHalfSeconds()
    {
        var plan = Plan();
        var body = KartAt(plan, 10, 0f, 0f); // held still, as against a wall
        var bot = new Bot { Skill = 0.5f, Sample = -1 };
        var controls = new KartControls();

        for (var i = 0; i < 175; i++)
            BotDriverSystem.Drive(ref bot, ref controls, body, Surface.Asphalt, Tuning, plan, Step);
        Assert.That(controls.Brake, Is.Zero, "still trying at 1.46 s");

        for (var i = 0; i < 10; i++)
            BotDriverSystem.Drive(ref bot, ref controls, body, Surface.Asphalt, Tuning, plan, Step);
        Assert.That(controls.Brake, Is.EqualTo(1f), "reversing");
        Assert.That(controls.Throttle, Is.Zero);
    }
}
