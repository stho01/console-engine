using System;
using System.Collections.Generic;
using ConsoleEngine.Ecs.Physics;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs.Physics;

public class SpatialHashGridTests
{
    [Test]
    public void FindPairs_BigBodiesSharingManyCells_ReportsPairOnce()
    {
        var bodies = new[] { Box(0, 0, 10), Box(3, 3, 10) };

        var pairs = FindPairs(bodies, cellSize: 2f);

        Assert.That(pairs, Is.EqualTo(new[] { (0, 1) }));
    }

    [Test]
    public void FindPairs_NegativeCoordinates_FindsOverlap()
    {
        var bodies = new[] { Box(-5.2f, -7.9f, .5f), Box(-4.8f, -7.5f, .5f) };

        Assert.That(FindPairs(bodies, cellSize: 1f), Is.EqualTo(new[] { (0, 1) }));
    }

    [Test]
    public void FindPairs_SameCellButBoxesApart_ReportsNothing()
    {
        var bodies = new[] { Box(1, 1, .2f), Box(3, 3, .2f) };

        Assert.That(FindPairs(bodies, cellSize: 8f), Is.Empty);
    }

    [Test]
    public void FindPairs_PairOrder_LowerIndexFirst()
    {
        var bodies = new[] { Box(50, 50, 1), Box(0, 0, 1), Box(.5f, .5f, 1) };

        Assert.That(FindPairs(bodies, cellSize: 4f), Is.EqualTo(new[] { (1, 2) }));
    }

    [Test]
    public void FindPairs_RandomBodies_MatchesAllPairsTest()
    {
        var random = new Random(7);
        var bodies = new BodyProxy[300];
        for (var i = 0; i < bodies.Length; i++)
        {
            var halfSize = .3f + random.NextSingle() * 3;
            bodies[i] = Box(random.NextSingle() * 60 - 30, random.NextSingle() * 60 - 30, halfSize);
        }

        var expected = new List<(int, int)>();
        for (var a = 0; a < bodies.Length; a++)
        for (var b = a + 1; b < bodies.Length; b++)
        {
            if (Overlaps(bodies[a], bodies[b]))
                expected.Add((a, b));
        }

        var actual = FindPairs(bodies, cellSize: 2.5f);
        actual.Sort();

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Build_Twice_ReusesStateAndFindsNewPairs()
    {
        var grid = new SpatialHashGrid(4f);
        var pairs = new ArrayBuffer<BodyPair>();
        grid.Build([Box(0, 0, 1), Box(1, 0, 1)]);
        grid.FindPairs([Box(0, 0, 1), Box(1, 0, 1)], pairs);

        var moved = new[] { Box(0, 0, 1), Box(20, 0, 1), Box(20.5f, 0, 1) };
        pairs.Clear();
        grid.Build(moved);
        grid.FindPairs(moved, pairs);

        Assert.That(pairs.Count, Is.EqualTo(1));
        Assert.That((pairs[0].A, pairs[0].B), Is.EqualTo((1, 2)));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    public void Ctor_InvalidCellSize_Throws(float cellSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SpatialHashGrid(cellSize));

    private static BodyProxy Box(float x, float y, float halfSize) => new()
    {
        Shape = ShapeType.Box,
        Center = new Vector2(x, y),
        Extents = new Vector2(halfSize, halfSize)
    };

    private static bool Overlaps(in BodyProxy a, in BodyProxy b) =>
        a.Min.X <= b.Max.X && b.Min.X <= a.Max.X && a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y;

    private static List<(int, int)> FindPairs(BodyProxy[] bodies, float cellSize)
    {
        var grid = new SpatialHashGrid(cellSize);
        var pairs = new ArrayBuffer<BodyPair>();
        grid.Build(bodies);
        grid.FindPairs(bodies, pairs);

        var result = new List<(int, int)>();
        foreach (var pair in pairs.AsSpan())
            result.Add((pair.A, pair.B));
        return result;
    }
}
