using System;
using System.Collections.Generic;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using KartViewer.Driving;
using KartViewer.Systems;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace KartViewer.Tests;

public class KartPhysicsSystemTests
{
    private const float Step = 1f / 120f;

    private static IEnumerable<TestCaseData> Karts()
    {
        yield return new TestCaseData(KartCatalog.Kart).SetArgDisplayNames("Kart");
        yield return new TestCaseData(KartCatalog.Wagon).SetArgDisplayNames("Wagon");
        yield return new TestCaseData(KartCatalog.Kart with { YawInertiaScale = 1.5f })
            .SetArgDisplayNames("KartWithYawInertiaScale");
    }

    [TestCaseSource(nameof(Karts))]
    public void Step_FullThrottleThenFullRightSteer_MatchesKartIntegrator(KartTuning tuning)
    {
        using var world = new World();
        var kart = KartEntity.Create(world, tuning, Vector2.Zero, 0f);
        var group = Physics(world, kart, tuning);
        var dynamics = new KartDynamics(tuning);
        var reference = new KartBody();

        for (var step = 0; step < 480; step++)
        {
            var controls = new KartControls { Throttle = 1f, Steer = step < 240 ? 0f : 1f };
            world.Get<KartControls>(kart) = controls;
            group.Update(Step);

            var forces = dynamics.ComputeForces(reference, controls, Surface.Asphalt, Step);
            KartIntegrator.Step(ref reference, forces, dynamics.Mass, dynamics.YawInertia, Step);
        }

        var ecs = Body(world, kart);
        var distance = System.Numerics.Vector2.Distance(ecs.Position, reference.Position);
        var degrees = MathF.Abs(ecs.Heading - reference.Heading) * 180f / MathF.PI;
        TestContext.Out.WriteLine($"{distance * 100f:0.0000} cm, {degrees:0.0000}°, reference at " +
                                  $"{reference.Position}, heading {reference.Heading * 180f / MathF.PI:0.0}°");
        Assert.That(reference.Heading, Is.LessThan(-0.5f), "the manoeuvre turns right");
        Assert.That(distance, Is.LessThan(0.01f));
        Assert.That(degrees, Is.LessThan(0.5f));
    }

    [Test]
    public void Step_InSteadyState_DoesNotAllocate()
    {
        using var world = new World();
        var tuning = KartCatalog.Kart;
        var kart = KartEntity.Create(world, tuning, Vector2.Zero, 0f);
        var group = Physics(world, kart, tuning);
        world.Get<KartControls>(kart) = new KartControls { Throttle = 1f, Steer = 0.5f };
        for (var step = 0; step < 120; step++)
            group.Update(Step);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 240; step++)
            group.Update(Step);

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    [Test]
    public void Step_KartTorqueLeft_TurnsCounterClockwiseSeenFromAbove()
    {
        using var world = new World();
        var kart = KartEntity.Create(world, KartCatalog.Kart, Vector2.Zero, 0f);
        var group = new SystemGroup(world);
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });

        world.Get<Force>(kart).AddTorque(KartFrame.ToPhysicsTorque(500f)); // left, in the kart frame
        group.Update(Step);
        group.Update(Step);

        var body = Body(world, kart);
        Assert.That(world.Get<Rotation>(kart).Radians, Is.LessThan(0f)); // counter-clockwise on screen = from above
        Assert.That(body.Heading, Is.GreaterThan(0f));
        Assert.That(body.AngularVelocity, Is.GreaterThan(0f));
        Assert.That(body.Forward.X, Is.LessThan(0f)); // left of -Z is -X
    }

    private static SystemGroup Physics(World world, Entity kart, KartTuning tuning)
    {
        var group = new SystemGroup(world);
        group.Add(new KartPhysicsSystem(kart, tuning));
        group.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        return group;
    }

    private static KartBody Body(World world, Entity kart) => KartFrame.ToBody(world.Get<Position>(kart).Value,
        world.Get<Rotation>(kart).Radians, world.Get<Velocity>(kart).Value, world.Get<AngularVelocity>(kart).Value);
}
