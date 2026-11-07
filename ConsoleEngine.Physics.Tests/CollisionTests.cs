using System;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

public class CollisionTests
{
    private const float Tolerance = 1e-5f;

    [Test]
    public void CircleCircle_Overlapping_NormalFromAToBAndNegativeSeparation()
    {
        var separation = Collision.CircleCircle(new Vector2(0, 0), 1f, new Vector2(1.5f, 0), 1f, out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(Vector2.UnitX));
            Assert.That(separation, Is.EqualTo(-.5f).Within(Tolerance));
        });
    }

    [Test]
    public void CircleCircle_Touching_ZeroSeparation()
    {
        var separation = Collision.CircleCircle(new Vector2(0, 0), 1f, new Vector2(0, 2), 1f, out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(Vector2.UnitY));
            Assert.That(separation, Is.Zero.Within(Tolerance));
        });
    }

    [Test]
    public void CircleCircle_Apart_PositiveGap() =>
        Assert.That(Collision.CircleCircle(Vector2.Zero, 1f, new Vector2(3, 0), 1f, out _),
            Is.EqualTo(1f).Within(Tolerance));

    [Test]
    public void CircleCircle_SameCenter_PicksUnitNormal()
    {
        var separation = Collision.CircleCircle(Vector2.Zero, 1f, Vector2.Zero, 2f, out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal.Length(), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(separation, Is.EqualTo(-3f).Within(Tolerance));
        });
    }

    [Test]
    public void BoxBox_OverlapSmallerOnX_PushesAlongX()
    {
        // Overlap 0.5 on x, 2 on y.
        var separation = Collision.BoxBox(new Vector2(0, 0), new Vector2(1, 1), new Vector2(-1.5f, 0), new Vector2(1, 1),
            out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(-Vector2.UnitX));
            Assert.That(separation, Is.EqualTo(-.5f).Within(Tolerance));
        });
    }

    [Test]
    public void BoxBox_OverlapSmallerOnY_PushesAlongY()
    {
        var separation = Collision.BoxBox(new Vector2(0, 0), new Vector2(2, 1), new Vector2(.5f, 1.75f),
            new Vector2(2, 1), out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(Vector2.UnitY));
            Assert.That(separation, Is.EqualTo(-.25f).Within(Tolerance));
        });
    }

    [Test]
    public void BoxBox_ApartDiagonally_LargerAxisGap()
    {
        // Gap 0.5 on x and 2 on y: the y axis separates them.
        var separation = Collision.BoxBox(Vector2.Zero, Vector2.One, new Vector2(2.5f, 4), Vector2.One, out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(Vector2.UnitY));
            Assert.That(separation, Is.EqualTo(2f).Within(Tolerance));
        });
    }

    [Test]
    public void CircleBox_CircleBesideBox_NormalFromCircleToBox()
    {
        // Circle left of a box whose left side is at x = 1.
        var separation = Collision.CircleBox(new Vector2(.5f, 0), 1f, new Vector2(2, 0), new Vector2(1, 1),
            out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(Vector2.UnitX));
            Assert.That(separation, Is.EqualTo(-.5f).Within(Tolerance));
        });
    }

    [Test]
    public void CircleBox_NearCorner_GapToCorner()
    {
        // Distance to the corner (1, 1) is sqrt(2).
        var separation = Collision.CircleBox(new Vector2(2, 2), 1f, Vector2.Zero, Vector2.One, out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(separation, Is.EqualTo(MathF.Sqrt(2f) - 1f).Within(Tolerance));
            Assert.That(normal.X, Is.EqualTo(-MathF.Sqrt(.5f)).Within(Tolerance));
            Assert.That(normal.Y, Is.EqualTo(-MathF.Sqrt(.5f)).Within(Tolerance));
        });
    }

    [Test]
    public void CircleBox_CenterInsideBox_LeavesThroughNearestSide()
    {
        // Centre 0.25 below the top side (y = -1) of the box.
        var separation = Collision.CircleBox(new Vector2(0, -.75f), .5f, Vector2.Zero, Vector2.One, out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(Vector2.UnitY), "points from the circle into the box");
            Assert.That(separation, Is.EqualTo(-.75f).Within(Tolerance));
        });
    }

    [Test]
    public void Collide_BoxThenCircle_NormalStillFromAToB()
    {
        var box = new BodyProxy { Shape = ShapeType.Box, Center = new Vector2(2, 0), Extents = Vector2.One };
        var circle = new BodyProxy { Shape = ShapeType.Circle, Center = new Vector2(.5f, 0), Extents = Vector2.One };

        var separation = Collision.Collide(box, circle, out var normal);

        Assert.Multiple(() =>
        {
            Assert.That(separation, Is.EqualTo(-.5f).Within(Tolerance));
            Assert.That(normal, Is.EqualTo(-Vector2.UnitX));
        });
    }
}
