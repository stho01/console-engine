using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

public class PhysicsSystemTests
{
    private const float Step = .5f;

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 4f));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    //**********************************************************
    //** integration
    //**********************************************************

    [Test]
    public void Update_DynamicBody_AppliesGravityThenMoves()
    {
        _physics.Gravity = new Vector2(0, 10);
        var body = _world.Create(new Position(Vector2.Zero), new Velocity(new Vector2(2, 0)),
            RigidBody.Dynamic(gravityScale: 2f));

        _group.Update(Step);

        // 4 substeps of 0.125 s: vy = 2.5, 5, 7.5, 10, y = 0.125 * (2.5 + 5 + 7.5 + 10).
        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Velocity>(body).Value, Is.EqualTo(new Vector2(2, 10)));
            Assert.That(_world.Get<Position>(body).Value, Is.EqualTo(new Vector2(1, 3.125f)));
        });
    }

    [Test]
    public void Update_DynamicBodyWithCollider_IntegratesLikeOneWithout()
    {
        _physics.Gravity = new Vector2(0, 10);
        var plain = _world.Create(new Position(Vector2.Zero), new Velocity(new Vector2(2, 0)), RigidBody.Dynamic());
        var shaped = _world.Create(new Position(new Vector2(100, 0)), new Velocity(new Vector2(2, 0)),
            RigidBody.Dynamic(), Collider.Circle(1f));

        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Velocity>(shaped).Value, Is.EqualTo(_world.Get<Velocity>(plain).Value));
            Assert.That(_world.Get<Position>(shaped).Value - new Vector2(100, 0),
                Is.EqualTo(_world.Get<Position>(plain).Value));
        });
    }

    [Test]
    public void Update_DynamicBodyWithDamping_SlowsDown()
    {
        var body = _world.Create(new Position(Vector2.Zero), new Velocity(new Vector2(6, 0)),
            RigidBody.Dynamic(linearDamping: 2f));

        _group.Update(Step);

        // Each of 4 substeps multiplies by 1 / (1 + 0.125 * 2) = 0.8.
        Assert.That(_world.Get<Velocity>(body).Value.X, Is.EqualTo(6f * .8f * .8f * .8f * .8f).Within(1e-4f));
    }

    [Test]
    public void Update_KinematicBody_MovesWithoutGravity()
    {
        _physics.Gravity = new Vector2(0, 10);
        var body = _world.Create(new Position(Vector2.Zero), new Velocity(new Vector2(2, 0)), RigidBody.Kinematic());

        _group.Update(Step);

        Assert.That(_world.Get<Position>(body).Value, Is.EqualTo(new Vector2(1, 0)));
    }

    [Test]
    public void Update_StaticBodyWithVelocity_DoesNotMove()
    {
        var body = _world.Create(new Position(Vector2.One), new Velocity(new Vector2(2, 0)), RigidBody.Static());

        _group.Update(Step);

        Assert.That(_world.Get<Position>(body).Value, Is.EqualTo(Vector2.One));
    }

    //**********************************************************
    //** contact events
    //**********************************************************

    [Test]
    public void Update_BodiesStartOverlapping_ReportsContactBeginOnce()
    {
        var a = CreateDynamicCircle(new Vector2(0, 0));
        var b = CreateDynamicCircle(new Vector2(1, 0));

        _group.Update(Step);
        var first = _physics.ContactBeginEvents.ToArray();
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Length.EqualTo(1));
            Assert.That((first[0].A, first[0].B), Is.EqualTo((a, b)));
            Assert.That(_physics.ContactBeginEvents.Length, Is.Zero, "no second begin while still touching");
            Assert.That(_physics.TouchingPairCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Update_BodiesSeparate_ReportsContactEnd()
    {
        var a = CreateDynamicCircle(new Vector2(0, 0));
        var b = CreateDynamicCircle(new Vector2(1, 0));
        _group.Update(Step);

        _world.Get<Position>(b).Value = new Vector2(10, 0);
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_physics.ContactEndEvents.Length, Is.EqualTo(1));
            Assert.That((_physics.ContactEndEvents[0].A, _physics.ContactEndEvents[0].B), Is.EqualTo((a, b)));
            Assert.That(_physics.TouchingPairCount, Is.Zero);
        });
    }

    [Test]
    public void Update_BodyDestroyedWhileTouching_ReportsContactEndWithDeadEntity()
    {
        CreateDynamicCircle(new Vector2(0, 0));
        var b = CreateDynamicCircle(new Vector2(1, 0));
        _group.Update(Step);

        _world.Destroy(b);
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_physics.ContactEndEvents.Length, Is.EqualTo(1));
            Assert.That(_world.IsAlive(_physics.ContactEndEvents[0].B), Is.False);
        });
    }

    [Test]
    public void Update_IdReusedByNewEntity_EndsOldPairAndBeginsNew()
    {
        CreateDynamicCircle(new Vector2(0, 0));
        var old = CreateDynamicCircle(new Vector2(1, 0));
        _group.Update(Step);

        _world.Destroy(old);
        var reused = CreateDynamicCircle(new Vector2(1, 0));
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(reused.Id, Is.EqualTo(old.Id), "precondition: the id was reused");
            Assert.That(_physics.ContactEndEvents.Length, Is.EqualTo(1));
            Assert.That(_physics.ContactEndEvents[0].B, Is.EqualTo(old));
            Assert.That(_physics.ContactBeginEvents.Length, Is.EqualTo(1));
            Assert.That(_physics.ContactBeginEvents[0].B, Is.EqualTo(reused));
        });
    }

    [Test]
    public void Update_EventsFromEarlierStep_ClearedOnNextStep()
    {
        CreateDynamicCircle(new Vector2(0, 0));
        CreateDynamicCircle(new Vector2(1, 0));
        _group.Update(Step);

        _group.Update(Step);

        Assert.That(_physics.ContactBeginEvents.Length, Is.Zero);
    }

    //**********************************************************
    //** pair rules
    //**********************************************************

    [Test]
    public void Update_FiltersRejectEachOther_NoEvent()
    {
        _world.Create(new Position(Vector2.Zero), Collider.Circle(1f) with { Category = 2, Mask = 4 },
            RigidBody.Dynamic(), new Velocity(Vector2.Zero));
        _world.Create(new Position(Vector2.One), Collider.Circle(1f) with { Category = 4, Mask = 1 },
            RigidBody.Dynamic(), new Velocity(Vector2.Zero));

        _group.Update(Step);

        Assert.That(_physics.TouchingPairCount, Is.Zero);
    }

    [Test]
    public void Update_FiltersAcceptEachOther_Touches()
    {
        _world.Create(new Position(Vector2.Zero), Collider.Circle(1f) with { Category = 2, Mask = 4 },
            RigidBody.Dynamic(), new Velocity(Vector2.Zero));
        _world.Create(new Position(Vector2.One), Collider.Circle(1f) with { Category = 4, Mask = 2 },
            RigidBody.Dynamic(), new Velocity(Vector2.Zero));

        _group.Update(Step);

        Assert.That(_physics.TouchingPairCount, Is.EqualTo(1));
    }

    [Test]
    public void Update_TwoStaticColliders_NeverTested()
    {
        // A collider without a RigidBody is static.
        _world.Create(new Position(Vector2.Zero), Collider.Box(Vector2.One));
        _world.Create(new Position(Vector2.One), Collider.Box(Vector2.One));

        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_physics.BodyCount, Is.EqualTo(2));
            Assert.That(_physics.TouchingPairCount, Is.Zero);
        });
    }

    [Test]
    public void Update_TwoKinematicBodies_NoContact()
    {
        CreateKinematic(Vector2.Zero, Collider.Circle(1f));
        CreateKinematic(Vector2.One, Collider.Circle(1f));

        _group.Update(Step);

        Assert.That(_physics.TouchingPairCount, Is.Zero);
    }

    [Test]
    public void Update_KinematicSensorOverDynamic_ReportsSensorBeginWithRoles()
    {
        var sensor = CreateKinematic(Vector2.Zero, Collider.Circle(1f) with { IsSensor = true });
        var visitor = CreateDynamicCircle(new Vector2(.5f, 0));

        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_physics.ContactBeginEvents.Length, Is.Zero);
            Assert.That(_physics.SensorBeginEvents.Length, Is.EqualTo(1));
            Assert.That(_physics.SensorBeginEvents[0].Sensor, Is.EqualTo(sensor));
            Assert.That(_physics.SensorBeginEvents[0].Visitor, Is.EqualTo(visitor));
        });
    }

    [Test]
    public void Update_SensorWithHigherIdThanVisitor_KeepsRoles()
    {
        var visitor = CreateDynamicCircle(new Vector2(.5f, 0));
        var sensor = CreateKinematic(Vector2.Zero, Collider.Circle(1f) with { IsSensor = true });

        _group.Update(Step);
        _world.Get<Position>(visitor).Value = new Vector2(20, 0);
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_physics.SensorEndEvents.Length, Is.EqualTo(1));
            Assert.That(_physics.SensorEndEvents[0].Sensor, Is.EqualTo(sensor));
            Assert.That(_physics.SensorEndEvents[0].Visitor, Is.EqualTo(visitor));
        });
    }

    [Test]
    public void Update_KinematicSensorOverStaticCollider_ReportsSensorBegin()
    {
        CreateKinematic(Vector2.Zero, Collider.Circle(1f) with { IsSensor = true });
        _world.Create(new Position(Vector2.One), Collider.Box(Vector2.One));

        _group.Update(Step);

        Assert.That(_physics.SensorBeginEvents.Length, Is.EqualTo(1));
    }

    [Test]
    public void Update_TwoSensors_NoEvent()
    {
        CreateKinematic(Vector2.Zero, Collider.Circle(1f) with { IsSensor = true });
        _world.Create(new Position(Vector2.One), new Velocity(Vector2.Zero), RigidBody.Dynamic(),
            Collider.Circle(1f) with { IsSensor = true });

        _group.Update(Step);

        Assert.That(_physics.TouchingPairCount, Is.Zero);
    }

    [Test]
    public void Update_ColliderOffset_UsesOffsetCenter()
    {
        CreateDynamicCircle(Vector2.Zero);
        _world.Create(new Position(new Vector2(10, 0)), Collider.Box(Vector2.One, offset: new Vector2(-8.5f, 0)));

        _group.Update(Step);

        Assert.That(_physics.TouchingPairCount, Is.EqualTo(1));
    }

    [Test]
    public void Update_ManyBodiesInALine_EachNeighbourPairOnce()
    {
        // Circles of radius 1, 1.5 apart: each touches only its neighbours.
        for (var i = 0; i < 100; i++)
            CreateDynamicCircle(new Vector2(i * 1.5f, 0));

        _group.Update(Step);

        Assert.That(_physics.ContactBeginEvents.Length, Is.EqualTo(99));
    }

    //**********************************************************
    //** static broadphase
    //**********************************************************

    [Test]
    public void Update_StaticBodyMovedByGameCode_TouchesAtItsNewPlace()
    {
        _world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), RigidBody.Dynamic(gravityScale: 0f),
            Collider.Circle(1f));
        var wall = _world.Create(new Position(new Vector2(20, 0)), Collider.Box(new Vector2(1, 1)));
        _group.Update(Step);
        Assert.That(_physics.TouchingPairCount, Is.Zero);

        _world.Get<Position>(wall).Value = new Vector2(1.5f, 0);
        _group.Update(Step);

        Assert.That(_physics.TouchingPairCount, Is.EqualTo(1));
    }

    [Test]
    public void Update_StaticBodyTurnedOrReshaped_UsesItsNewBounds()
    {
        _world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), RigidBody.Dynamic(gravityScale: 0f),
            Collider.Circle(1f));
        var wall = _world.Create(new Position(new Vector2(0, 6)), new Rotation(0f), Collider.Box(new Vector2(1, 1)));
        _group.Update(Step);
        Assert.That(_physics.TouchingPairCount, Is.Zero);

        _world.Get<Collider>(wall) = Collider.Box(new Vector2(1, 5.5f));
        _group.Update(Step);
        Assert.That(_physics.TouchingPairCount, Is.EqualTo(1), "grown towards the ball");

        _world.Get<Rotation>(wall).Radians = MathF.PI / 2f;
        _group.Update(Step);
        Assert.That(_physics.TouchingPairCount, Is.Zero, "turned away from it");
    }

    [Test]
    public void Update_StaticBodyAddedThenDestroyed_TouchesOnlyWhileItExists()
    {
        _world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), RigidBody.Dynamic(gravityScale: 0f),
            Collider.Circle(1f));
        _group.Update(Step);

        var wall = _world.Create(new Position(new Vector2(1.5f, 0)), Collider.Box(new Vector2(1, 1)));
        _group.Update(Step);
        Assert.That(_physics.TouchingPairCount, Is.EqualTo(1));

        _world.Destroy(wall);
        _group.Update(Step);
        Assert.That(_physics.TouchingPairCount, Is.Zero);
    }

    [Test]
    public void Update_BodyBeforeStaticDestroyed_StaticKeepsCollidingAndBeingHit()
    {
        // The first body's archetype comes first, so destroying it moves the wall to a lower body index.
        var far = _world.Create(new Position(new Vector2(-50, 0)), new Velocity(Vector2.Zero),
            RigidBody.Dynamic(gravityScale: 0f), Collider.Circle(1f));
        var wall = _world.Create(new Position(new Vector2(1.5f, 0)), Collider.Box(new Vector2(1, 1)));
        _world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), RigidBody.Dynamic(gravityScale: 0f),
            Collider.Circle(1f));
        _group.Update(Step);

        _world.Destroy(far);
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_physics.TouchingPairCount, Is.EqualTo(1));
            Assert.That(_physics.Raycast(new Vector2(10, 0), new Vector2(-20, 0), Collider.AllCategories, out var hit),
                Is.True);
            Assert.That(hit.Entity, Is.EqualTo(wall));
        });
    }

    [Test]
    public void Update_ManyUnchangedStatics_KeepCollidingAndBeingHitStepAfterStep()
    {
        // Turned boxes and circles that no step changes: after the first step they are reused, not read again.
        for (var i = 0; i < 300; i++)
        {
            _world.Create(new Position(new Vector2(i * 4f, 10)), new Rotation(i * .3f), Collider.Box(new Vector2(1, .5f)));
            _world.Create(new Position(new Vector2(i * 4f, -10)), Collider.Circle(1f));
        }

        var target = _world.Create(new Position(new Vector2(600, 0)), new Rotation(.5f), Collider.Box(new Vector2(1, 1)));
        var ball = _world.Create(new Position(new Vector2(590, 0)), new Velocity(new Vector2(10, 0)),
            RigidBody.Dynamic(gravityScale: 0f), Collider.Circle(1f));
        for (var i = 0; i < 20; i++)
            _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Position>(ball).Value.X, Is.LessThan(600f), "stopped by the target");
            Assert.That(_physics.Raycast(new Vector2(700, 0), new Vector2(-150, 0), Collider.AllCategories, out var hit),
                Is.True);
            Assert.That(hit.Entity, Is.EqualTo(target));
            Assert.That(_physics.Raycast(new Vector2(1196, 0), new Vector2(0, 20), Collider.AllCategories, out hit),
                Is.True, "the last turned box");
        });
    }

    //**********************************************************
    //** components
    //**********************************************************

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.PositiveInfinity)]
    public void RigidBodyDynamic_InvalidMass_Throws(float mass) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RigidBody.Dynamic(mass));

    [Test]
    public void RigidBodyDefault_IsStatic() => Assert.That(default(RigidBody).Type, Is.EqualTo(BodyType.Static));

    [Test]
    public void Update_MovementSystemWithoutRigidBodies_EachEntityMovedOnce()
    {
        _group.Add(new MovementSystem(Query.Without<RigidBody>()));
        var body = _world.Create(new Position(Vector2.Zero), new Velocity(new Vector2(2, 0)), RigidBody.Kinematic());
        var plain = _world.Create(new Position(Vector2.Zero), new Velocity(new Vector2(2, 0)));

        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Position>(body).Value, Is.EqualTo(new Vector2(1, 0)), "moved by physics only");
            Assert.That(_world.Get<Position>(plain).Value, Is.EqualTo(new Vector2(1, 0)), "moved by MovementSystem");
        });
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private Entity CreateDynamicCircle(Vector2 position) =>
        _world.Create(new Position(position), new Velocity(Vector2.Zero), RigidBody.Dynamic(), Collider.Circle(1f));

    private Entity CreateKinematic(Vector2 position, Collider collider) =>
        _world.Create(new Position(position), new Velocity(Vector2.Zero), RigidBody.Kinematic(), collider);
}
