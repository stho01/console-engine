using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

/// <summary>Forces and torques on bodies: accumulated, held through one step, then cleared. y points down.</summary>
public class ForceTests
{
    private const float Step = 1f / 60f;
    private const float Mass = 2f;
    private static readonly Vector2 HalfExtents = new(1f, .5f);

    private World _world;
    private SystemGroup _group;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _group.Add(new PhysicsSystem());
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Update_ConstantForce_AcceleratesByForceOverMass()
    {
        var box = CreateBox(RigidBody.Dynamic(Mass));
        _world.Get<Force>(box).Add(new Vector2(6f, -4f));

        _group.Update(Step);

        var velocity = _world.Get<Velocity>(box).Value;
        Assert.Multiple(() =>
        {
            Assert.That(velocity.X, Is.EqualTo(6f / Mass * Step).Within(1e-6f));
            Assert.That(velocity.Y, Is.EqualTo(-4f / Mass * Step).Within(1e-6f));
            Assert.That(_world.Get<Position>(box).Value.X, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void Update_Torque_SpinsByTorqueOverInertia()
    {
        var box = CreateBox(RigidBody.Dynamic(Mass));
        _world.Get<Force>(box).AddTorque(3f);

        _group.Update(Step);

        // A box's inertia about its centre: m (w² + h²) / 12 = m (hx² + hy²) / 3.
        var inertia = Mass * HalfExtents.LengthSquared() / 3f;
        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<AngularVelocity>(box).Value, Is.EqualTo(3f / inertia * Step).Within(1e-6f));
            Assert.That(_world.Get<Velocity>(box).Value, Is.EqualTo(Vector2.Zero));
        });
    }

    [Test]
    public void Update_ForceDownOnRightSide_PushesDownAndTurnsClockwise()
    {
        var box = CreateBox(RigidBody.Dynamic(Mass));
        _world.Get<Force>(box).AddAtPoint(new Vector2(0f, 10f), new Vector2(1f, 0f), Vector2.Zero);

        _group.Update(Step);

        var inertia = Mass * HalfExtents.LengthSquared() / 3f;
        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Velocity>(box).Value.Y, Is.EqualTo(10f / Mass * Step).Within(1e-6f));
            Assert.That(_world.Get<AngularVelocity>(box).Value, Is.EqualTo(10f / inertia * Step).Within(1e-6f));
            Assert.That(_world.Get<Rotation>(box).Radians, Is.GreaterThan(0f), "clockwise on screen");
        });
    }

    [Test]
    public void AddAtPoint_ForceThroughCenter_AddsNoTorque()
    {
        var force = new Force();

        force.AddAtPoint(new Vector2(3f, 0f), new Vector2(5f, 2f), new Vector2(1f, 2f));

        Assert.Multiple(() =>
        {
            Assert.That(force.Value, Is.EqualTo(new Vector2(3f, 0f)));
            Assert.That(force.Torque, Is.Zero);
        });
    }

    [Test]
    public void Update_AfterStep_ClearsForce()
    {
        var box = CreateBox(RigidBody.Dynamic(Mass));
        _world.Get<Force>(box).AddAtPoint(new Vector2(0f, 10f), new Vector2(1f, 0f), Vector2.Zero);
        _group.Update(Step);
        var velocity = _world.Get<Velocity>(box).Value;
        var spin = _world.Get<AngularVelocity>(box).Value;

        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Force>(box).Value, Is.EqualTo(Vector2.Zero));
            Assert.That(_world.Get<Force>(box).Torque, Is.Zero);
            Assert.That(_world.Get<Velocity>(box).Value, Is.EqualTo(velocity));
            Assert.That(_world.Get<AngularVelocity>(box).Value, Is.EqualTo(spin));
        });
    }

    [Test]
    public void Update_ForceOnSleepingBody_WakesAndMovesIt()
    {
        var body = RigidBody.Dynamic(Mass);
        body.IsSleeping = true;
        var box = CreateBox(body);
        _group.Update(Step);
        Assume.That(_world.Get<RigidBody>(box).IsSleeping, Is.True);

        _world.Get<Force>(box).Add(new Vector2(10f, 0f));
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<RigidBody>(box).IsSleeping, Is.False);
            Assert.That(_world.Get<Velocity>(box).Value.X, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void Update_TorqueOnSleepingBody_WakesIt()
    {
        var body = RigidBody.Dynamic(Mass);
        body.IsSleeping = true;
        var box = CreateBox(body);

        _world.Get<Force>(box).AddTorque(1f);
        _group.Update(Step);

        Assert.That(_world.Get<AngularVelocity>(box).Value, Is.GreaterThan(0f));
    }

    [TestCase(BodyType.Kinematic)]
    [TestCase(BodyType.Static)]
    public void Update_ForceOnNonDynamicBody_IsIgnoredAndCleared(BodyType type)
    {
        var box = CreateBox(type == BodyType.Kinematic ? RigidBody.Kinematic() : RigidBody.Static());
        _world.Get<Force>(box).AddAtPoint(new Vector2(10f, 10f), new Vector2(1f, 0f), Vector2.Zero);

        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Position>(box).Value, Is.EqualTo(Vector2.Zero));
            Assert.That(_world.Get<Velocity>(box).Value, Is.EqualTo(Vector2.Zero));
            Assert.That(_world.Get<AngularVelocity>(box).Value, Is.Zero);
            Assert.That(_world.Get<Force>(box).Value, Is.EqualTo(Vector2.Zero));
        });
    }

    [Test]
    public void Update_TorqueOnBodyWithoutRotation_IsIgnored()
    {
        var box = _world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), RigidBody.Dynamic(Mass),
            Collider.Box(HalfExtents), new Force { Torque = 5f });

        _group.Update(Step);

        Assert.That(_world.Get<Velocity>(box).Value, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void Update_ForceOnBodyWithoutCollider_AcceleratesByForceOverMass()
    {
        var body = _world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), RigidBody.Dynamic(Mass),
            new Force { Value = new Vector2(0f, 8f) });

        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Velocity>(body).Value.Y, Is.EqualTo(8f / Mass * Step).Within(1e-6f));
            Assert.That(_world.Get<Position>(body).Value.Y, Is.GreaterThan(0f));
            Assert.That(_world.Get<Force>(body).Value, Is.EqualTo(Vector2.Zero));
        });
    }

    [Test]
    public void Update_ForcesEveryStep_DoesNotAllocate()
    {
        var box = CreateBox(RigidBody.Dynamic(Mass));
        var free = _world.Create(new Position(new Vector2(20f, 0f)), new Velocity(Vector2.Zero),
            RigidBody.Dynamic(Mass), new Force());
        for (var i = 0; i < 10; i++)
            StepWithForces(box, free); // warm-up

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10; i++)
            StepWithForces(box, free);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    private void StepWithForces(Entity box, Entity free)
    {
        _world.Get<Force>(box).AddAtPoint(new Vector2(0f, 1f), new Vector2(1f, 0f), _world.Get<Position>(box).Value);
        _world.Get<Force>(free).Add(new Vector2(1f, 0f));
        _group.Update(Step);
    }

    private Entity CreateBox(RigidBody body)
    {
        var box = _world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), new Rotation(0f),
            new AngularVelocity(0f), body, Collider.Box(HalfExtents));
        _world.Add(box, new Force());
        return box;
    }
}
