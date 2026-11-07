using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

/// <summary>Islands of still bodies going to sleep and waking up. y points down.</summary>
public class SleepTests
{
    private const float Step = 1f / 60f;
    private const float GroundTop = 10f;

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;
    private Entity _ground;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 4f) { Gravity = new Vector2(0, 50) });
        _ground = _world.Create(new Position(new Vector2(0, GroundTop + 1)), Collider.Box(new Vector2(50, 1)));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Update_BoxRestingLongEnough_FallsAsleep()
    {
        var box = CreateBox(new Vector2(0, GroundTop - .5f));

        RunSeconds(2f);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<RigidBody>(box).IsSleeping, Is.True);
            Assert.That(_physics.SleepingBodyCount, Is.EqualTo(1));
            Assert.That(_world.Get<Velocity>(box).Value, Is.EqualTo(Vector2.Zero));
        });
    }

    [Test]
    public void Update_FallingBox_StaysAwake()
    {
        var box = CreateBox(new Vector2(0, -100));

        RunSeconds(1f);

        Assert.That(_world.Get<RigidBody>(box).IsSleeping, Is.False);
    }

    [Test]
    public void Update_SleepingBoxGivenVelocity_WakesAndMoves()
    {
        var box = CreateBox(new Vector2(0, GroundTop - .5f));
        RunSeconds(2f);

        _world.Get<Velocity>(box).Value = new Vector2(0, -20);
        _group.Update(Step);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<RigidBody>(box).IsSleeping, Is.False);
            Assert.That(_world.Get<Position>(box).Value.Y, Is.LessThan(GroundTop - .6f));
        });
    }

    [Test]
    public void Update_SleepingStackHitByBall_WholeStackWakes()
    {
        var bottom = CreateBox(new Vector2(0, GroundTop - .5f));
        var top = CreateBox(new Vector2(0, GroundTop - 1.5f));
        RunSeconds(2f);
        Assume.That(_world.Get<RigidBody>(top).IsSleeping, Is.True);

        _world.Create(new Position(new Vector2(-10, GroundTop - 1.5f)), new Velocity(new Vector2(40, 0)),
            RigidBody.Dynamic(), Collider.Circle(.5f));
        RunSeconds(.5f);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<RigidBody>(top).IsSleeping, Is.False, "hit directly");
            Assert.That(_world.Get<RigidBody>(bottom).IsSleeping, Is.False, "same island");
        });
    }

    [Test]
    public void Update_SupportDestroyed_SleepingBoxWakesAndFalls()
    {
        var shelf = _world.Create(new Position(new Vector2(0, 0)), Collider.Box(new Vector2(2, .5f)));
        var box = CreateBox(new Vector2(0, -1f));
        RunSeconds(2f);
        Assume.That(_world.Get<RigidBody>(box).IsSleeping, Is.True);

        _world.Destroy(shelf);
        RunSeconds(.5f);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<RigidBody>(box).IsSleeping, Is.False);
            Assert.That(_world.Get<Position>(box).Value.Y, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void Update_SleepDisabled_NeverSleeps()
    {
        _physics.EnableSleep = false;
        var box = CreateBox(new Vector2(0, GroundTop - .5f));

        RunSeconds(2f);

        Assert.That(_world.Get<RigidBody>(box).IsSleeping, Is.False);
    }

    [Test]
    public void Update_SleepingPair_NoEndEventWhileAsleep()
    {
        CreateBox(new Vector2(0, GroundTop - .5f));
        var ends = 0;

        for (var i = 0; i < 180; i++)
        {
            _group.Update(Step);
            ends += _physics.ContactEndEvents.Length;
        }

        Assert.That(ends, Is.Zero);
    }

    [Test]
    public void Update_SleepingPile_SolvesNoContacts()
    {
        for (var i = 0; i < 5; i++)
            CreateBox(new Vector2(i * 1.5f, GroundTop - .5f));

        RunSeconds(3f);

        Assert.Multiple(() =>
        {
            Assert.That(_physics.SleepingBodyCount, Is.EqualTo(5));
            Assert.That(_physics.ContactCount, Is.Zero);
            Assert.That(_world.IsAlive(_ground), Is.True);
        });
    }

    private Entity CreateBox(Vector2 position) =>
        _world.Create(new Position(position), new Velocity(Vector2.Zero), RigidBody.Dynamic(),
            Collider.Box(new Vector2(.5f, .5f)));

    private void RunSeconds(float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
            _group.Update(Step);
    }
}
