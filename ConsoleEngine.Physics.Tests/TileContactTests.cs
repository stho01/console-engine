using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

/// <summary>Dynamic bodies against a <see cref="TileGrid"/>: 30×12 tiles of 1×1, floor on row 10, side walls.</summary>
public class TileContactTests
{
    private const float Step = 1f / 60f;
    private const float FloorTop = 10f;

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 2f) { Gravity = new Vector2(0, 50), EnableSleep = false });
        var tiles = new TileGrid(30, 12, Vector2.One);
        for (var column = 0; column < 30; column++)
        {
            tiles.SetSolid(column, 10);
            tiles.SetSolid(column, 11);
        }

        for (var row = 0; row < 12; row++)
        {
            tiles.SetSolid(0, row);
            tiles.SetSolid(29, row);
        }

        _physics.TileGrid = tiles;
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Update_BallDroppedOnTiles_RestsOnTop()
    {
        var ball = _world.Create(new Position(new Vector2(5, 2)), new Velocity(Vector2.Zero), RigidBody.Dynamic(),
            Collider.Circle(.5f));

        RunSeconds(3f);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Position>(ball).Value.Y, Is.EqualTo(FloorTop - .5f).Within(.02f));
            Assert.That(_world.Get<Velocity>(ball).Value.Length(), Is.LessThan(.05f));
        });
    }

    [Test]
    public void Update_FastBallIntoTileFloor_DoesNotTunnel()
    {
        var ball = _world.Create(new Position(new Vector2(5, 2)), new Velocity(new Vector2(0, 900)),
            RigidBody.Dynamic(), Collider.Circle(.5f));

        RunSeconds(1f);

        Assert.That(_world.Get<Position>(ball).Value.Y, Is.LessThan(FloorTop));
    }

    [Test]
    public void Update_BoxSlidingOverTileSeams_KeepsSpeed()
    {
        var box = _world.Create(new Position(new Vector2(3, FloorTop - .5f)), new Velocity(new Vector2(10, 0)),
            RigidBody.Dynamic(), Collider.Box(new Vector2(.5f, .5f)) with { Friction = 0f });

        RunSeconds(1f);

        // 1 s at 10 cells/s crosses 10 seams between tiles.
        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Velocity>(box).Value.X, Is.EqualTo(10f).Within(.05f));
            Assert.That(_world.Get<Position>(box).Value.X, Is.EqualTo(13f).Within(.1f));
        });
    }

    [Test]
    public void Update_BoxWithFrictionOnTiles_Stops()
    {
        var box = _world.Create(new Position(new Vector2(3, FloorTop - .5f)), new Velocity(new Vector2(10, 0)),
            RigidBody.Dynamic(), Collider.Box(new Vector2(.5f, .5f)));

        RunSeconds(2f);

        Assert.That(_world.Get<Velocity>(box).Value.X, Is.EqualTo(0f).Within(.05f));
    }

    [Test]
    public void Update_BallIntoTileWall_BouncesOff()
    {
        var ball = _world.Create(new Position(new Vector2(20, FloorTop - .5f)), new Velocity(new Vector2(30, 0)),
            RigidBody.Dynamic(), Collider.Circle(.5f) with { Restitution = 1f, Friction = 0f });

        RunSeconds(1f);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Velocity>(ball).Value.X, Is.LessThan(-20f));
            Assert.That(_world.Get<Position>(ball).Value.X, Is.LessThan(29f - .5f + .02f));
        });
    }

    [Test]
    public void Update_MaskWithoutTileCategory_FallsThroughTiles()
    {
        var ghost = _world.Create(new Position(new Vector2(5, 2)), new Velocity(Vector2.Zero), RigidBody.Dynamic(),
            Collider.Circle(.5f) with { Mask = 2 });

        RunSeconds(1f);

        Assert.That(_world.Get<Position>(ghost).Value.Y, Is.GreaterThan(12f));
    }

    [Test]
    public void Update_TurningBoxOnTiles_LiesFlat()
    {
        var box = _world.Create(new Position(new Vector2(8, 5)), new Velocity(Vector2.Zero), new Rotation(.6f),
            new AngularVelocity(0f), RigidBody.Dynamic(), Collider.Box(Vector2.One));

        RunSeconds(4f);

        var angle = _world.Get<Rotation>(box).Radians;
        Assert.That(MathF.Abs(MathF.Sin(2f * angle)), Is.LessThan(.02f), $"angle {angle}");
    }

    [TestCase(0f)]
    [TestCase(.3f)]
    [TestCase(-.3f)]
    public void Update_TriangleTipRisingIntoCeilingSeam_StaysBelow(float angle)
    {
        // A ceiling one tile thick on row 1 and gravity pointing up. The tip pokes into the seam between two tiles,
        // where each tile alone separates most along its side face, which touches the other tile. The tip must still be
        // pushed back down, not ignored.
        var tiles = new TileGrid(30, 12, Vector2.One);
        for (var column = 0; column < 30; column++)
            tiles.SetSolid(column, 1);
        _physics.TileGrid = tiles;
        _physics.Gravity = new Vector2(0, -50);
        var triangle = _physics.Polygons.Add([new Vector2(0, -1.6f), new Vector2(1.5f, 1f), new Vector2(-1.5f, 1f)]);
        var body = _world.Create(new Position(new Vector2(15f, 9)), new Velocity(new Vector2(0f, -15f)),
            new Rotation(angle), new AngularVelocity(0f), RigidBody.Dynamic(2f), Collider.Polygon(triangle));

        RunSeconds(3f);

        Assert.That(_world.Get<Position>(body).Value.Y, Is.GreaterThan(2f));
    }

    private void RunSeconds(float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
            _group.Update(Step);
    }
}
