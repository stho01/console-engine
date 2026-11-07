using System;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

public class CollisionTests
{
    private const float Tolerance = 1e-4f;

    //**********************************************************
    //** circles
    //**********************************************************

    [Test]
    public void CircleCircle_Overlapping_OnePointHalfwayAndNegativeSeparation()
    {
        Collision.CircleCircle(new Vector2(0, 0), 1f, new Vector2(1.5f, 0), 1f, 0f, out var manifold);

        Assert.Multiple(() =>
        {
            Assert.That(manifold.PointCount, Is.EqualTo(1));
            Assert.That(manifold.Normal, Is.EqualTo(Vector2.UnitX));
            Assert.That(manifold.Point0.Separation, Is.EqualTo(-.5f).Within(Tolerance));
            Assert.That(manifold.Point0.Point.X, Is.EqualTo(.75f).Within(Tolerance));
        });
    }

    [Test]
    public void CircleCircle_GapWithinSpeculativeDistance_KeepsPoint()
    {
        Collision.CircleCircle(Vector2.Zero, 1f, new Vector2(3, 0), 1f, speculative: 2f, out var manifold);

        Assert.Multiple(() =>
        {
            Assert.That(manifold.PointCount, Is.EqualTo(1));
            Assert.That(manifold.Point0.Separation, Is.EqualTo(1f).Within(Tolerance));
        });
    }

    [Test]
    public void CircleCircle_GapBeyondSpeculativeDistance_NoPoints()
    {
        Collision.CircleCircle(Vector2.Zero, 1f, new Vector2(3, 0), 1f, speculative: .5f, out var manifold);

        Assert.That(manifold.PointCount, Is.Zero);
    }

    [Test]
    public void PolygonCircle_CircleOnFace_NormalFromPolygon()
    {
        var box = ConvexPolygon.Box(Vector2.Zero, Vector2.One);

        Collision.PolygonCircle(box, new Vector2(0, -1.5f), 1f, 0f, out var manifold);

        Assert.Multiple(() =>
        {
            Assert.That(manifold.Normal, Is.EqualTo(new Vector2(0, -1)));
            Assert.That(manifold.Point0.Separation, Is.EqualTo(-.5f).Within(Tolerance));
        });
    }

    [Test]
    public void PolygonCircle_NearCorner_NormalFromCorner()
    {
        var box = ConvexPolygon.Box(Vector2.Zero, Vector2.One);

        Collision.PolygonCircle(box, new Vector2(1.5f, 1.5f), 1f, 0f, out var manifold);

        Assert.Multiple(() =>
        {
            Assert.That(manifold.PointCount, Is.EqualTo(1));
            Assert.That(manifold.Normal.X, Is.EqualTo(MathF.Sqrt(.5f)).Within(Tolerance));
            Assert.That(manifold.Point0.Separation, Is.EqualTo(MathF.Sqrt(.5f) - 1f).Within(Tolerance));
        });
    }

    [Test]
    public void PolygonCircle_CornerOutOfReach_NoPoints()
    {
        var box = ConvexPolygon.Box(Vector2.Zero, Vector2.One);

        // 1.41 from the corner, radius 1.3.
        Collision.PolygonCircle(box, new Vector2(2, 2), 1.3f, 0f, out var manifold);

        Assert.That(manifold.PointCount, Is.Zero);
    }

    //**********************************************************
    //** polygons
    //**********************************************************

    [Test]
    public void Polygons_BoxRestingOnBox_TwoPointsAtTheCorners()
    {
        var floor = ConvexPolygon.Box(new Vector2(0, 2), new Vector2(5, 1));
        var box = ConvexPolygon.Box(new Vector2(0, .5f), new Vector2(.5f, .5f));

        Collision.Polygons(box, floor, 0f, out var manifold);

        Assert.Multiple(() =>
        {
            Assert.That(manifold.PointCount, Is.EqualTo(2));
            Assert.That(manifold.Normal.Y, Is.EqualTo(1f).Within(Tolerance), "from the box into the floor");
            Assert.That(manifold.Point0.Separation, Is.Zero.Within(Tolerance));
            Assert.That(MathF.Abs(manifold.Point0.Point.X - manifold.Point1.Point.X), Is.EqualTo(1f).Within(Tolerance));
        });
    }

    [Test]
    public void Polygons_TurnedBoxCornerIntoFloor_OnePoint()
    {
        var floor = ConvexPolygon.Box(new Vector2(0, 2), new Vector2(5, 1));
        var (sin, cos) = MathF.SinCos(MathF.PI / 4f);
        var diamond = ConvexPolygon.Box(Vector2.Zero, new Vector2(.5f, .5f)).Transform(new Vector2(0, .4f), cos, sin);

        Collision.Polygons(diamond, floor, 0f, out var manifold);

        Assert.Multiple(() =>
        {
            Assert.That(manifold.PointCount, Is.EqualTo(1));
            Assert.That(manifold.Normal.Y, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(manifold.Point0.Separation, Is.EqualTo(1f - (.4f + MathF.Sqrt(.5f))).Within(Tolerance));
        });
    }

    [Test]
    public void Polygons_Apart_NoPoints()
    {
        var a = ConvexPolygon.Box(Vector2.Zero, Vector2.One);
        var b = ConvexPolygon.Box(new Vector2(3, 0), Vector2.One);

        Collision.Polygons(a, b, 0f, out var manifold);

        Assert.That(manifold.PointCount, Is.Zero);
    }

    [Test]
    public void Collide_TurnedBoxAgainstUnturned_NormalFromAToB()
    {
        var turned = new BodyShape { Position = new Vector2(-1.9f, 0), Shape = ShapeType.Box, HalfExtents = Vector2.One };
        (turned.Sin, turned.Cos) = MathF.SinCos(.1f);
        var plain = BodyShape.Unrotated(Vector2.Zero, ShapeType.Box);
        plain.HalfExtents = Vector2.One;

        Collision.Collide(turned, plain, new PolygonLibrary(), 0f, out var manifold);

        Assert.Multiple(() =>
        {
            Assert.That(manifold.PointCount, Is.GreaterThan(0));
            Assert.That(manifold.Normal.X, Is.GreaterThan(.9f));
        });
    }

    //**********************************************************
    //** polygon library
    //**********************************************************

    [Test]
    public void PolygonLibrary_Add_TakesHullAndDropsInnerPoints()
    {
        var library = new PolygonLibrary();

        var id = library.Add([new Vector2(0, 0), new Vector2(2, 0), new Vector2(1, .5f), new Vector2(2, 2),
            new Vector2(0, 2)]);

        Assert.That(library.GetVertices(id), Has.Length.EqualTo(4));
    }

    [Test]
    public void PolygonLibrary_SquareAroundOrigin_InertiaMatchesBox()
    {
        var library = new PolygonLibrary();

        var id = library.Add([new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1)]);

        // A 2×2 box: (w² + h²) / 12 = 8 / 12 per unit mass.
        Assert.That(library.InertiaPerMass(id), Is.EqualTo(8f / 12f).Within(Tolerance));
    }

    [Test]
    public void PolygonLibrary_TooManyCorners_Throws()
    {
        var points = new Vector2[10];
        for (var i = 0; i < points.Length; i++)
            points[i] = new Vector2(MathF.Cos(i * MathF.Tau / 10), MathF.Sin(i * MathF.Tau / 10));

        Assert.Throws<ArgumentException>(() => new PolygonLibrary().Add(points));
    }
}
