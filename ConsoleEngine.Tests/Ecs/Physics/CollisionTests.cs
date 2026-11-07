using ConsoleEngine.Ecs.Physics;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs.Physics;

public class CollisionTests
{
    private const float Tolerance = 1e-5f;

    [Test]
    public void CircleCircle_Overlapping_NormalFromAToBAndNegativeSeparation()
    {
        var hit = Collision.CircleCircle(new Vector2(0, 0), 1f, new Vector2(1.5f, 0), 1f, out var normal,
            out var separation);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(normal, Is.EqualTo(Vector2.UnitX));
            Assert.That(separation, Is.EqualTo(-.5f).Within(Tolerance));
        });
    }

    [Test]
    public void CircleCircle_Touching_CollidesWithZeroSeparation()
    {
        var hit = Collision.CircleCircle(new Vector2(0, 0), 1f, new Vector2(0, 2), 1f, out var normal,
            out var separation);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(normal, Is.EqualTo(Vector2.UnitY));
            Assert.That(separation, Is.Zero.Within(Tolerance));
        });
    }

    [Test]
    public void CircleCircle_Apart_ReturnsFalse() =>
        Assert.That(Collision.CircleCircle(Vector2.Zero, 1f, new Vector2(2.01f, 0), 1f, out _, out _), Is.False);

    [Test]
    public void CircleCircle_SameCenter_PicksUnitNormal()
    {
        Collision.CircleCircle(Vector2.Zero, 1f, Vector2.Zero, 2f, out var normal, out var separation);

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
        var hit = Collision.BoxBox(new Vector2(0, 0), new Vector2(1, 1), new Vector2(-1.5f, 0), new Vector2(1, 1),
            out var normal, out var separation);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(normal, Is.EqualTo(-Vector2.UnitX));
            Assert.That(separation, Is.EqualTo(-.5f).Within(Tolerance));
        });
    }

    [Test]
    public void BoxBox_OverlapSmallerOnY_PushesAlongY()
    {
        var hit = Collision.BoxBox(new Vector2(0, 0), new Vector2(2, 1), new Vector2(.5f, 1.75f), new Vector2(2, 1),
            out var normal, out var separation);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(normal, Is.EqualTo(Vector2.UnitY));
            Assert.That(separation, Is.EqualTo(-.25f).Within(Tolerance));
        });
    }

    [Test]
    public void BoxBox_Apart_ReturnsFalse() =>
        Assert.That(Collision.BoxBox(Vector2.Zero, Vector2.One, new Vector2(0, 2.5f), Vector2.One, out _, out _),
            Is.False);

    [Test]
    public void CircleBox_CircleBesideBox_NormalFromCircleToBox()
    {
        // Circle left of a box whose left side is at x = 1.
        var hit = Collision.CircleBox(new Vector2(.5f, 0), 1f, new Vector2(2, 0), new Vector2(1, 1), out var normal,
            out var separation);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(normal, Is.EqualTo(Vector2.UnitX));
            Assert.That(separation, Is.EqualTo(-.5f).Within(Tolerance));
        });
    }

    [Test]
    public void CircleBox_NearCornerButOutsideRadius_ReturnsFalse() =>
        // Distance to the corner (1, 1) is about 1.41.
        Assert.That(Collision.CircleBox(new Vector2(2, 2), 1.3f, Vector2.Zero, Vector2.One, out _, out _), Is.False);

    [Test]
    public void CircleBox_CenterInsideBox_LeavesThroughNearestSide()
    {
        // Centre 0.25 below the top side (y = -1) of the box.
        Collision.CircleBox(new Vector2(0, -.75f), .5f, Vector2.Zero, Vector2.One, out var normal, out var separation);

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

        var hit = Collision.Collide(box, circle, out var normal, out _);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(normal, Is.EqualTo(-Vector2.UnitX));
        });
    }
}
