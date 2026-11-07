using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

/// <summary>A 1×1 character in a 20×10 room of 1×1 tiles: floor on row 9, walls in columns 0 and 19.</summary>
public class CharacterMoverTests
{
    private const float Step = 1f / 60f;
    private static readonly Vector2 Half = new(.5f, .5f);

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 2f));
        var tiles = new TileGrid(20, 10, Vector2.One);
        for (var column = 0; column < 20; column++)
            tiles.SetSolid(column, 9);
        for (var row = 0; row < 10; row++)
        {
            tiles.SetSolid(0, row);
            tiles.SetSolid(19, row);
        }

        _physics.TileGrid = tiles;
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void MoveAndSlide_FallingOntoFloor_StopsOnTopAndReportsFloor()
    {
        var center = new Vector2(5, 5);
        var velocity = new Vector2(0, 30);
        var collisions = SlideCollisions.None;

        // With gravity, as a game runs it: once standing, every step still pushes into the floor.
        for (var i = 0; i < 30; i++)
        {
            velocity.Y += 50f * Step;
            collisions = CharacterMover.MoveAndSlide(_physics, ref center, ref velocity, Half, Step);
        }

        Assert.Multiple(() =>
        {
            Assert.That(center.Y, Is.EqualTo(9f - .5f - CharacterMover.SkinWidth).Within(1e-3f));
            Assert.That(velocity.Y, Is.Zero);
            Assert.That(collisions, Is.EqualTo(SlideCollisions.Floor));
        });
    }

    [Test]
    public void MoveAndSlide_DiagonalIntoFloor_SlidesAlongIt()
    {
        var center = new Vector2(5, 8.4f);
        var velocity = new Vector2(60, 60);

        var collisions = CharacterMover.MoveAndSlide(_physics, ref center, ref velocity, Half, Step);

        Assert.Multiple(() =>
        {
            Assert.That(center.X, Is.EqualTo(6f).Within(1e-3f), "keeps the full horizontal move");
            Assert.That(center.Y, Is.LessThan(8.5f));
            Assert.That(velocity, Is.EqualTo(new Vector2(60, 0)));
            Assert.That(collisions, Is.EqualTo(SlideCollisions.Floor));
        });
    }

    [Test]
    public void MoveAndSlide_RunningAlongFloor_NoCollisionAtTileSeams()
    {
        var center = new Vector2(3, 9f - .5f - CharacterMover.SkinWidth);
        var velocity = new Vector2(30, 0);
        var collisions = SlideCollisions.None;

        for (var i = 0; i < 20; i++)
            collisions |= CharacterMover.MoveAndSlide(_physics, ref center, ref velocity, Half, Step);

        Assert.Multiple(() =>
        {
            Assert.That(collisions, Is.EqualTo(SlideCollisions.None));
            Assert.That(center.X, Is.EqualTo(13f).Within(1e-3f));
        });
    }

    [Test]
    public void MoveAndSlide_IntoWall_StopsAndReportsWall()
    {
        var center = new Vector2(17, 5);
        var velocity = new Vector2(120, 0);

        var collisions = CharacterMover.MoveAndSlide(_physics, ref center, ref velocity, Half, Step);

        Assert.Multiple(() =>
        {
            Assert.That(center.X, Is.EqualTo(19f - .5f - CharacterMover.SkinWidth).Within(1e-3f));
            Assert.That(velocity.X, Is.Zero);
            Assert.That(collisions, Is.EqualTo(SlideCollisions.Wall));
        });
    }

    [Test]
    public void MoveAndSlide_JumpIntoCeilingBody_ReportsCeiling()
    {
        _world.Create(new Position(new Vector2(5, 2)), Collider.Box(new Vector2(3, .5f)));
        _group.Update(Step);
        var center = new Vector2(5, 3.6f);
        var velocity = new Vector2(0, -60);

        var collisions = CharacterMover.MoveAndSlide(_physics, ref center, ref velocity, Half, Step);

        Assert.Multiple(() =>
        {
            Assert.That(collisions, Is.EqualTo(SlideCollisions.Ceiling));
            Assert.That(velocity.Y, Is.Zero);
            Assert.That(center.Y, Is.EqualTo(3f + CharacterMover.SkinWidth).Within(1e-3f));
        });
    }

    [Test]
    public void MoveAndSlide_FastFall_DoesNotTunnelThroughFloor()
    {
        var center = new Vector2(5, 2);
        var velocity = new Vector2(0, 2000);

        CharacterMover.MoveAndSlide(_physics, ref center, ref velocity, Half, Step);

        Assert.That(center.Y, Is.LessThan(8.5f + 1e-3f));
    }

    [Test]
    public void MoveAndSlide_OwnColliderIgnored_Moves()
    {
        var self = _world.Create(new Position(new Vector2(5, 5)), Collider.Box(Half));
        _group.Update(Step);
        var center = new Vector2(5, 5);
        var velocity = new Vector2(60, 0);

        var collisions = CharacterMover.MoveAndSlide(_physics, ref center, ref velocity, Half, Step, ignore: self);

        Assert.Multiple(() =>
        {
            Assert.That(collisions, Is.EqualTo(SlideCollisions.None));
            Assert.That(center.X, Is.EqualTo(6f).Within(1e-4f));
        });
    }
}
