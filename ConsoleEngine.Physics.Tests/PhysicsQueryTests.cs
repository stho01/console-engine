using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

/// <summary>Raycasts, box casts and overlaps through <see cref="IPhysicsQueries"/>, after one physics step.</summary>
public class PhysicsQueryTests
{
    private const float Step = 1f / 60f;
    private const float Tolerance = 1e-4f;

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 2f));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    //**********************************************************
    //** raycast
    //**********************************************************

    [Test]
    public void Raycast_ThroughTwoBodies_ReportsClosest()
    {
        var near = CreateStaticBox(new Vector2(10, 0), Vector2.One);
        CreateStaticBox(new Vector2(20, 0), Vector2.One);
        _group.Update(Step);

        var found = _physics.Raycast(Vector2.Zero, new Vector2(40, 0), Collider.AllCategories, out var hit);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(hit.Entity, Is.EqualTo(near));
            Assert.That(hit.Point.X, Is.EqualTo(9f).Within(Tolerance));
            Assert.That(hit.Normal, Is.EqualTo(-Vector2.UnitX));
            Assert.That(hit.Fraction, Is.EqualTo(9f / 40f).Within(Tolerance));
        });
    }

    [Test]
    public void Raycast_Circle_HitsSurfaceWithRadialNormal()
    {
        CreateStaticCircle(new Vector2(0, 10), 2f);
        _group.Update(Step);

        _physics.Raycast(Vector2.Zero, new Vector2(0, 20), Collider.AllCategories, out var hit);

        Assert.Multiple(() =>
        {
            Assert.That(hit.Point.Y, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(hit.Normal.Y, Is.EqualTo(-1f).Within(Tolerance));
        });
    }

    [Test]
    public void Raycast_TooShort_Misses()
    {
        CreateStaticBox(new Vector2(10, 0), Vector2.One);
        _group.Update(Step);

        Assert.That(_physics.Raycast(Vector2.Zero, new Vector2(8, 0), Collider.AllCategories, out _), Is.False);
    }

    [Test]
    public void Raycast_OriginInsideBody_IgnoresIt()
    {
        CreateStaticBox(Vector2.Zero, new Vector2(2, 2));
        _group.Update(Step);

        Assert.That(_physics.Raycast(Vector2.Zero, new Vector2(10, 0), Collider.AllCategories, out _), Is.False);
    }

    [Test]
    public void Raycast_MaskExcludesCategory_Misses()
    {
        _world.Create(new Position(new Vector2(10, 0)), Collider.Box(Vector2.One) with { Category = 4 });
        _group.Update(Step);

        Assert.That(_physics.Raycast(Vector2.Zero, new Vector2(20, 0), mask: 2, out _), Is.False);
    }

    [Test]
    public void Raycast_Sensor_NotReported()
    {
        _world.Create(new Position(new Vector2(10, 0)), Collider.Box(Vector2.One) with { IsSensor = true });
        _group.Update(Step);

        Assert.That(_physics.Raycast(Vector2.Zero, new Vector2(20, 0), Collider.AllCategories, out _), Is.False);
    }

    [Test]
    public void Raycast_BodyMovedDuringStep_SeesEndOfStepPosition()
    {
        _world.Create(new Position(new Vector2(10, 0)), new Velocity(new Vector2(60, 0)), RigidBody.Kinematic(),
            Collider.Box(Vector2.One));
        _group.Update(Step);

        _physics.Raycast(Vector2.Zero, new Vector2(40, 0), Collider.AllCategories, out var hit);

        // Moved 1 cell in the step: its left side is at 10.
        Assert.That(hit.Point.X, Is.EqualTo(10f).Within(Tolerance));
    }

    [Test]
    public void Raycast_DiagonalAcrossTiles_HitsFirstSolidTileFace()
    {
        var tiles = new TileGrid(10, 10, new Vector2(4, 4));
        tiles.SetSolid(5, 5);
        _physics.TileGrid = tiles;

        var found = _physics.Raycast(new Vector2(2, 22), new Vector2(40, 0), Collider.AllCategories, out var hit);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(hit.IsTile, Is.True);
            Assert.That(hit.Tile, Is.EqualTo(new Point(5, 5)));
            Assert.That(hit.Point.X, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(hit.Normal, Is.EqualTo(-Vector2.UnitX));
        });
    }

    [Test]
    public void Raycast_StartsInsideSolidTile_HitsNextWallAfterLeaving()
    {
        var tiles = new TileGrid(10, 1, new Vector2(1, 1));
        tiles.SetSolid(0, 0);
        tiles.SetSolid(5, 0);
        _physics.TileGrid = tiles;

        _physics.Raycast(new Vector2(.5f, .5f), new Vector2(9, 0), Collider.AllCategories, out var hit);

        Assert.That(hit.Tile, Is.EqualTo(new Point(5, 0)));
    }

    [Test]
    public void Raycast_BodyBeforeTile_ReportsBody()
    {
        var tiles = new TileGrid(10, 1, new Vector2(1, 1));
        tiles.SetSolid(8, 0);
        _physics.TileGrid = tiles;
        var body = CreateStaticBox(new Vector2(4, .5f), new Vector2(.5f, .5f));
        _group.Update(Step);

        _physics.Raycast(new Vector2(.5f, .5f), new Vector2(9, 0), Collider.AllCategories, out var hit);

        Assert.That(hit.Entity, Is.EqualTo(body));
    }

    //**********************************************************
    //** box cast
    //**********************************************************

    [Test]
    public void CastBox_IntoBox_StopsAtContact()
    {
        CreateStaticBox(new Vector2(10, 0), Vector2.One);
        _group.Update(Step);

        _physics.CastBox(Vector2.Zero, new Vector2(.5f, .5f), new Vector2(20, 0), Collider.AllCategories,
            Entity.Null, out var hit);

        Assert.Multiple(() =>
        {
            Assert.That(hit.Point.X, Is.EqualTo(8.5f).Within(Tolerance));
            Assert.That(hit.Normal, Is.EqualTo(-Vector2.UnitX));
        });
    }

    [Test]
    public void CastBox_PastCircleCorner_RoundedCornerMisses()
    {
        CreateStaticCircle(new Vector2(10, 0), 1f);
        _group.Update(Step);

        // The box's centre moves along x + y = 7.2, through the corner of the circle's bounds grown by the box
        // (8.5..11.5, -1.5..1.5) but 1.27 from the corner circle around (9.5, -0.5), radius 1: a plain grown-box test
        // would hit, the rounded corner doesn't.
        var found = _physics.CastBox(new Vector2(5.6f, 1.6f), new Vector2(.5f, .5f), new Vector2(4, -4),
            Collider.AllCategories, Entity.Null, out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void CastBox_IgnoredEntity_NotHit()
    {
        var self = CreateStaticBox(Vector2.Zero, new Vector2(.5f, .5f));
        _group.Update(Step);

        var found = _physics.CastBox(Vector2.Zero, new Vector2(.5f, .5f), new Vector2(0, 5), Collider.AllCategories,
            self, out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void CastBox_StartingInsideAndMovingOut_NotHit()
    {
        CreateStaticBox(Vector2.Zero, Vector2.One);
        _group.Update(Step);

        var found = _physics.CastBox(new Vector2(1.2f, 0), new Vector2(.5f, .5f), new Vector2(5, 0),
            Collider.AllCategories, Entity.Null, out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void CastBox_StartingInsideAndMovingIn_HitAtZero()
    {
        CreateStaticBox(Vector2.Zero, Vector2.One);
        _group.Update(Step);

        var found = _physics.CastBox(new Vector2(1.2f, 0), new Vector2(.5f, .5f), new Vector2(-5, 0),
            Collider.AllCategories, Entity.Null, out var hit);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(hit.Fraction, Is.Zero);
            Assert.That(hit.Normal, Is.EqualTo(Vector2.UnitX));
        });
    }

    [Test]
    public void CastBox_AlongRowOfTiles_DoesNotCatchOnSeams()
    {
        // A floor of single tiles; the box sits exactly on it and moves along it.
        var tiles = new TileGrid(20, 2, new Vector2(1, 1));
        for (var column = 0; column < 20; column++)
            tiles.SetSolid(column, 1);
        _physics.TileGrid = tiles;

        var found = _physics.CastBox(new Vector2(2, .5f), new Vector2(.5f, .5f), new Vector2(10, 0),
            Collider.AllCategories, Entity.Null, out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void CastBox_IntoTileWall_StopsAtWall()
    {
        var tiles = new TileGrid(20, 4, new Vector2(1, 1));
        for (var row = 0; row < 4; row++)
            tiles.SetSolid(10, row);
        _physics.TileGrid = tiles;

        _physics.CastBox(new Vector2(2, 1.5f), new Vector2(.5f, .5f), new Vector2(20, 0), Collider.AllCategories,
            Entity.Null, out var hit);

        Assert.Multiple(() =>
        {
            Assert.That(hit.Point.X, Is.EqualTo(9.5f).Within(Tolerance));
            Assert.That(hit.Normal, Is.EqualTo(-Vector2.UnitX));
        });
    }

    //**********************************************************
    //** overlap
    //**********************************************************

    [Test]
    public void OverlapBox_ReportsOverlappingOnlyOnce()
    {
        var big = CreateStaticBox(Vector2.Zero, new Vector2(5, 5));
        var small = CreateStaticCircle(new Vector2(6, 0), 1.5f);
        CreateStaticBox(new Vector2(30, 0), Vector2.One);
        _group.Update(Step);
        Span<Entity> results = stackalloc Entity[8];

        var count = _physics.OverlapBox(new Vector2(4, 0), new Vector2(1, 1), Collider.AllCategories, results);

        Assert.That(results[..count].ToArray(), Is.EquivalentTo(new[] { big, small }));
    }

    [Test]
    public void OverlapCircle_ResultsFull_StopsAtCapacity()
    {
        for (var i = 0; i < 5; i++)
            CreateStaticCircle(new Vector2(i * .1f, 0), 1f);
        _group.Update(Step);
        Span<Entity> results = stackalloc Entity[3];

        var count = _physics.OverlapCircle(Vector2.Zero, 1f, Collider.AllCategories, results);

        Assert.That(count, Is.EqualTo(3));
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private Entity CreateStaticBox(Vector2 center, Vector2 halfExtents) =>
        _world.Create(new Position(center), Collider.Box(halfExtents));

    private Entity CreateStaticCircle(Vector2 center, float radius) =>
        _world.Create(new Position(center), Collider.Circle(radius));
}
