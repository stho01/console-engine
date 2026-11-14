using System;
using ConsoleEngine.Ecs.Components;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace KartViewer.Tests;

public class KartFrameTests
{
    [TestCase(0f)]
    [TestCase(0.7f)]
    [TestCase(-2.5f)]
    public void ToBody_AnyRotation_FacesWhereThePhysicsRotationFaces(float radians)
    {
        var body = KartFrame.ToBody(Vector2.Zero, radians, Vector2.Zero, 0f);
        var heading = new Rotation(radians).Heading;

        Assert.That(body.Forward.X, Is.EqualTo(heading.X).Within(1e-6f));
        Assert.That(body.Forward.Y, Is.EqualTo(heading.Y).Within(1e-6f));
    }

    [Test]
    public void ToBody_ThenBack_GivesThePhysicsStateAgain()
    {
        var body = KartFrame.ToBody(new Vector2(3f, -4f), 0.6f, new Vector2(5f, 6f), -1.25f);

        Assert.That(body.Position, Is.EqualTo(new System.Numerics.Vector2(3f, -4f)));
        Assert.That(body.Velocity, Is.EqualTo(new System.Numerics.Vector2(5f, 6f)));
        Assert.That(KartFrame.ToRadians(body.Heading), Is.EqualTo(0.6f));
        Assert.That(-body.AngularVelocity, Is.EqualTo(-1.25f));
        Assert.That(KartFrame.ToPhysicsForce(new System.Numerics.Vector2(7f, -8f)), Is.EqualTo(new Vector2(7f, -8f)));
        Assert.That(KartFrame.ToPhysicsTorque(9f), Is.EqualTo(-9f));
    }

    [Test]
    public void HeadingOf_MinusZ_IsZeroAndMinusX_IsAQuarterTurnLeft()
    {
        Assert.That(KartFrame.HeadingOf(-Vector3.UnitZ), Is.EqualTo(0f));
        Assert.That(KartFrame.HeadingOf(-Vector3.UnitX), Is.EqualTo(MathF.PI / 2f).Within(1e-6f));
    }
}
