using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Physics;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs.Physics;

/// <summary>Contact response through whole steps at 60 Hz, as a game runs it. y points down.</summary>
public class PhysicsSolverTests
{
    private const float Step = 1f / 60f;

    /// <summary>The ground's top side.</summary>
    private const float GroundTop = 10f;

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 4f));
        _physics.Gravity = new Vector2(0, 50);
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Update_BallDroppedOnGround_ComesToRestOnTop()
    {
        CreateGround();
        var ball = CreateBall(new Vector2(0, 0), radius: .5f);

        RunSeconds(3f);

        Assert.Multiple(() =>
        {
            Assert.That(PositionOf(ball).Y, Is.EqualTo(GroundTop - .5f).Within(.02f));
            Assert.That(VelocityOf(ball).Length(), Is.LessThan(.05f));
        });
    }

    [Test]
    public void Update_BallRestingOnGround_OneBeginNoEnd()
    {
        CreateGround();
        CreateBall(new Vector2(0, GroundTop - 2f), radius: .5f);
        var begins = 0;
        var ends = 0;

        for (var i = 0; i < 180; i++)
        {
            _group.Update(Step);
            begins += _physics.ContactBeginEvents.Length;
            ends += _physics.ContactEndEvents.Length;
        }

        Assert.Multiple(() =>
        {
            Assert.That(begins, Is.EqualTo(1));
            Assert.That(ends, Is.Zero, "a resting contact must not flicker");
        });
    }

    [Test]
    public void Update_FastBallTowardsThinWall_DoesNotTunnel()
    {
        _physics.Gravity = Vector2.Zero;
        _world.Create(new Position(new Vector2(20, 0)), Collider.Box(new Vector2(.25f, 5)));
        var ball = CreateBall(Vector2.Zero, radius: .5f, velocity: new Vector2(600, 0));

        RunSeconds(.5f);

        // 600 cells/s moves 10 cells per step: without speculative contacts it would jump the wall.
        Assert.That(PositionOf(ball).X, Is.LessThan(20f - .25f - .5f + .05f));
    }

    [Test]
    public void Update_BouncyBallHitsGround_BouncesBack()
    {
        CreateGround();
        var ball = CreateBall(new Vector2(0, GroundTop - 5f), radius: .5f, velocity: new Vector2(0, 20),
            restitution: 1f);
        var fastestUp = 0f;

        for (var i = 0; i < 30; i++)
        {
            _group.Update(Step);
            fastestUp = MathF.Max(fastestUp, -VelocityOf(ball).Y);
        }

        // Falls 4.5 cells starting at 20 cells/s: about 29 cells/s at impact.
        Assert.That(fastestUp, Is.GreaterThan(25f));
    }

    [Test]
    public void Update_DeadBallHitsGround_DoesNotBounce()
    {
        CreateGround();
        var ball = CreateBall(new Vector2(0, GroundTop - 5f), radius: .5f, velocity: new Vector2(0, 20));
        var fastestUp = 0f;

        for (var i = 0; i < 30; i++)
        {
            _group.Update(Step);
            fastestUp = MathF.Max(fastestUp, -VelocityOf(ball).Y);
        }

        Assert.That(fastestUp, Is.LessThan(1f));
    }

    [Test]
    public void Update_HeadOnEqualMasses_ConservesMomentum()
    {
        _physics.Gravity = Vector2.Zero;
        var left = CreateBall(new Vector2(0, 0), radius: 1f, velocity: new Vector2(10, 0));
        var right = CreateBall(new Vector2(5, 0), radius: 1f, velocity: new Vector2(-4, 0));

        RunSeconds(1f);

        Assert.Multiple(() =>
        {
            Assert.That(VelocityOf(left).X + VelocityOf(right).X, Is.EqualTo(6f).Within(1e-3f));
            Assert.That(VelocityOf(left).X, Is.EqualTo(3f).Within(.05f), "no restitution: they move on together");
            Assert.That(PositionOf(right).X - PositionOf(left).X, Is.GreaterThan(2f - .05f));
        });
    }

    [Test]
    public void Update_OverlappingBodies_PushedApartNoFasterThanPushSpeed()
    {
        _physics.Gravity = Vector2.Zero;
        var a = CreateBall(new Vector2(0, 0), radius: 1f);
        var b = CreateBall(new Vector2(1, 0), radius: 1f);

        _group.Update(Step);
        var pushedApart = PositionOf(b).X - PositionOf(a).X - 1f;
        var leftOver = VelocityOf(b).X - VelocityOf(a).X;
        RunSeconds(2f);

        // The push moves them but the relax pass takes the push speed back out, so they don't fly apart.
        Assert.Multiple(() =>
        {
            Assert.That(pushedApart, Is.GreaterThan(0f).And.LessThanOrEqualTo(_physics.ContactPushMaxSpeed * Step + 1e-4f));
            Assert.That(leftOver, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(PositionOf(b).X - PositionOf(a).X, Is.GreaterThan(2f - .05f), "apart in the end");
        });
    }

    [Test]
    public void Update_BoxSlidingWithFriction_Stops()
    {
        CreateGround();
        var box = CreateBox(new Vector2(0, GroundTop - .5f), velocity: new Vector2(10, 0), friction: .6f);

        RunSeconds(2f);

        // Friction 0.6 under gravity 50 decelerates at 30 cells/s²: stopped after a third of a second.
        Assert.That(VelocityOf(box).X, Is.EqualTo(0f).Within(.05f));
    }

    [Test]
    public void Update_BoxSlidingWithoutFriction_KeepsSpeed()
    {
        CreateGround(friction: 0f);
        var box = CreateBox(new Vector2(0, GroundTop - .5f), velocity: new Vector2(10, 0), friction: 0f);

        RunSeconds(1f);

        Assert.That(VelocityOf(box).X, Is.EqualTo(10f).Within(.05f));
    }

    [Test]
    public void Update_BoxOnKinematicPlatform_PlatformNotPushedAndBoxRidesAlong()
    {
        var platform = _world.Create(new Position(new Vector2(0, GroundTop + 1)), new Velocity(new Vector2(0, -2)),
            RigidBody.Kinematic(), Collider.Box(new Vector2(10, 1)));
        var box = CreateBox(new Vector2(0, GroundTop - .5f));

        RunSeconds(2f);

        Assert.Multiple(() =>
        {
            Assert.That(VelocityOf(platform), Is.EqualTo(new Vector2(0, -2)));
            Assert.That(PositionOf(platform).Y, Is.EqualTo(GroundTop + 1 - 4).Within(1e-3f));
            Assert.That(PositionOf(box).Y, Is.EqualTo(PositionOf(platform).Y - 1 - .5f).Within(.05f));
        });
    }

    [Test]
    public void Update_StackOfFiveBoxes_StaysStacked()
    {
        CreateGround();
        var boxes = new Entity[5];
        for (var i = 0; i < boxes.Length; i++)
            boxes[i] = CreateBox(new Vector2(0, GroundTop - .5f - i));

        RunSeconds(3f);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < boxes.Length; i++)
            {
                Assert.That(PositionOf(boxes[i]).Y, Is.EqualTo(GroundTop - .5f - i).Within(.1f), $"box {i}");
                Assert.That(VelocityOf(boxes[i]).Length(), Is.LessThan(.05f), $"box {i} at rest");
                Assert.That(PositionOf(boxes[i]).X, Is.EqualTo(0f).Within(.01f), $"box {i}");
            }
        });
    }

    [Test]
    public void Update_ShowerOfBallsInABox_SettlesInsideWithoutTunnelling()
    {
        // A box 20 wide (inner x 0..20) with the floor at GroundTop, like the sandbox's walls.
        CreateGround();
        _world.Create(new Position(new Vector2(-.5f, 0)), Collider.Box(new Vector2(.5f, 20)));
        _world.Create(new Position(new Vector2(20.5f, 0)), Collider.Box(new Vector2(.5f, 20)));
        var balls = new Entity[200];
        for (var i = 0; i < balls.Length; i++)
            balls[i] = CreateBall(new Vector2(.5f + i % 19, GroundTop - 1 - i / 19 * 1.2f), radius: .5f,
                velocity: new Vector2(i % 2 == 0 ? 8 : -8, 0), restitution: .6f);

        RunSeconds(6f);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < balls.Length; i++)
            {
                var position = PositionOf(balls[i]);
                Assert.That(position.X, Is.InRange(.5f - .1f, 19.5f + .1f), $"ball {i} x");
                Assert.That(position.Y, Is.LessThanOrEqualTo(GroundTop - .5f + .1f), $"ball {i} y");
                Assert.That(VelocityOf(balls[i]).Length(), Is.LessThan(1f), $"ball {i} settled");
            }
        });
    }

    [Test]
    public void Update_SubstepCountZero_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _physics.SubstepCount = 0);

    //**********************************************************
    //** helpers
    //**********************************************************

    private void RunSeconds(float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
            _group.Update(Step);
    }

    private Entity CreateGround(float friction = Collider.DefaultFriction) =>
        _world.Create(new Position(new Vector2(0, GroundTop + 1)),
            Collider.Box(new Vector2(50, 1)) with { Friction = friction });

    private Entity CreateBall(Vector2 position, float radius, Vector2 velocity = default, float restitution = 0f) =>
        _world.Create(new Position(position), new Velocity(velocity), RigidBody.Dynamic(),
            Collider.Circle(radius) with { Restitution = restitution });

    private Entity CreateBox(Vector2 position, Vector2 velocity = default, float friction = Collider.DefaultFriction) =>
        _world.Create(new Position(position), new Velocity(velocity), RigidBody.Dynamic(),
            Collider.Box(new Vector2(.5f, .5f)) with { Friction = friction });

    private Vector2 PositionOf(Entity entity) => _world.Get<Position>(entity).Value;
    private Vector2 VelocityOf(Entity entity) => _world.Get<Velocity>(entity).Value;
}
