using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

/// <summary>Bodies with <c>Rotation</c> and <see cref="AngularVelocity"/>, over whole steps at 60 Hz (y down).</summary>
public class RotationTests
{
    private const float Step = 1f / 60f;
    private const float GroundTop = 10f;

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 4f) { Gravity = new Vector2(0, 50), EnableSleep = false });
        _world.Create(new Position(new Vector2(0, GroundTop + 1)), Collider.Box(new Vector2(50, 1)));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Update_BoxDroppedOnCorner_TipsOverAndLiesFlat()
    {
        var box = CreateTurningBox(new Vector2(0, GroundTop - 3), angle: .5f);

        RunSeconds(4f);

        var angle = _world.Get<Rotation>(box).Radians;
        var flatness = MathF.Abs(MathF.Sin(2f * angle)); // 0 when the angle is a multiple of 90°.
        Assert.Multiple(() =>
        {
            Assert.That(flatness, Is.LessThan(.02f), $"angle {angle}");
            Assert.That(_world.Get<Position>(box).Value.Y, Is.EqualTo(GroundTop - 1f).Within(.05f));
            Assert.That(_world.Get<AngularVelocity>(box).Value, Is.EqualTo(0f).Within(.05f));
        });
    }

    [Test]
    public void Update_BallSlidingOnFloor_StartsRolling()
    {
        var ball = _world.Create(new Position(new Vector2(0, GroundTop - 1)), new Velocity(new Vector2(10, 0)),
            new Rotation(0f), new AngularVelocity(0f), RigidBody.Dynamic(), Collider.Circle(1f));

        RunSeconds(1f);

        // Rolling without slipping: the contact point stands still, so w * r equals v (clockwise when moving right).
        var speed = _world.Get<Velocity>(ball).Value.X;
        var spin = _world.Get<AngularVelocity>(ball).Value;
        Assert.Multiple(() =>
        {
            Assert.That(speed, Is.GreaterThan(1f));
            Assert.That(spin * 1f, Is.EqualTo(speed).Within(.05f));
        });
    }

    [Test]
    public void Update_TriangleDropped_ComesToRest()
    {
        var triangle = _physics.Polygons.Add([new Vector2(0, -1), new Vector2(1, .7f), new Vector2(-1, .7f)]);
        var body = _world.Create(new Position(new Vector2(0, GroundTop - 4)), new Velocity(Vector2.Zero),
            new Rotation(.3f), new AngularVelocity(0f), RigidBody.Dynamic(), Collider.Polygon(triangle));

        RunSeconds(4f);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Velocity>(body).Value.Length(), Is.LessThan(.05f));
            Assert.That(_world.Get<Position>(body).Value.Y, Is.LessThan(GroundTop));
        });
    }

    [Test]
    public void Update_BoxWithoutAngularVelocity_HitOffCentre_DoesNotTurn()
    {
        var box = _world.Create(new Position(new Vector2(0, GroundTop - 3)), new Velocity(Vector2.Zero),
            new Rotation(.5f), RigidBody.Dynamic(), Collider.Box(new Vector2(1, 1)));

        RunSeconds(2f);

        Assert.That(_world.Get<Rotation>(box).Radians, Is.EqualTo(.5f));
    }

    [Test]
    public void Update_KinematicSpinner_TurnsAtItsSpeed()
    {
        var spinner = _world.Create(new Position(new Vector2(20, 0)), new Velocity(Vector2.Zero), new Rotation(0f),
            new AngularVelocity(2f), RigidBody.Kinematic(), Collider.Box(new Vector2(3, .5f)));

        RunSeconds(.5f);

        Assert.That(_world.Get<Rotation>(spinner).Radians, Is.EqualTo(1f).Within(1e-3f));
    }

    [Test]
    public void Update_BodyWithoutCollider_Turns()
    {
        var body = _world.Create(new Position(new Vector2(20, 0)), new Velocity(Vector2.Zero), new Rotation(0f),
            new AngularVelocity(3f), RigidBody.Dynamic(gravityScale: 0f));

        RunSeconds(1f);

        Assert.That(_world.Get<Rotation>(body).Radians, Is.EqualTo(3f).Within(1e-3f));
    }

    [Test]
    public void Raycast_TurnedBox_HitsTurnedSurface()
    {
        // A diamond (box turned 45°) with half size 1: its left corner is at x = -sqrt(2).
        _world.Create(new Position(new Vector2(0, -20)), new Rotation(MathF.PI / 4f), Collider.Box(Vector2.One));
        _group.Update(Step);

        _physics.Raycast(new Vector2(-10, -20), new Vector2(20, 0), Collider.AllCategories, out var hit);

        Assert.That(hit.Point.X, Is.EqualTo(-MathF.Sqrt(2f)).Within(1e-3f));
    }

    [Test]
    public void CastBox_AgainstPolygon_StopsAtItsEdge()
    {
        var square = _physics.Polygons.Add([new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1),
            new Vector2(-1, 1)]);
        _world.Create(new Position(new Vector2(10, -20)), Collider.Polygon(square));
        _group.Update(Step);

        _physics.CastBox(new Vector2(0, -20), new Vector2(.5f, .5f), new Vector2(20, 0), Collider.AllCategories,
            Entity.Null, out var hit);

        Assert.Multiple(() =>
        {
            Assert.That(hit.Point.X, Is.EqualTo(8.5f).Within(1e-3f));
            Assert.That(hit.Normal.X, Is.EqualTo(-1f).Within(1e-3f));
        });
    }

    private Entity CreateTurningBox(Vector2 position, float angle) =>
        _world.Create(new Position(position), new Velocity(Vector2.Zero), new Rotation(angle), new AngularVelocity(0f),
            RigidBody.Dynamic(), Collider.Box(Vector2.One));

    private void RunSeconds(float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
            _group.Update(Step);
    }
}
