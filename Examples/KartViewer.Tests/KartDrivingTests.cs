using System.Numerics;
using NUnit.Framework;

namespace KartViewer.Tests;

public class KartDrivingTests
{
    private const float Step = 1f / 60;

    private static KartDriving Drive(int frames, bool throttle = true, bool brake = false, float steer = 0,
        bool onRoad = true)
    {
        var kart = new KartDriving(Vector3.Zero, -Vector3.UnitZ);
        for (var i = 0; i < frames; i++)
            kart.Update(throttle, brake, steer, onRoad, Step);
        return kart;
    }

    [Test]
    public void Update_ThrottleOnRoad_ReachesRoadTopSpeedAndMovesForward()
    {
        var kart = Drive(frames: 600);

        Assert.That(kart.Speed, Is.EqualTo(KartDriving.RoadTopSpeed).Within(0.01f));
        Assert.That(kart.Position.Z, Is.LessThan(-50f)); // -Z is forward
    }

    [Test]
    public void Update_ThrottleOnGrass_IsHeldToGrassTopSpeed()
    {
        var kart = Drive(frames: 600, onRoad: false);

        Assert.That(kart.Speed, Is.EqualTo(KartDriving.GrassTopSpeed).Within(0.01f));
    }

    [Test]
    public void Update_BrakeFromStandstill_Reverses()
    {
        var kart = Drive(frames: 600, throttle: false, brake: true);

        Assert.That(kart.Speed, Is.EqualTo(-KartDriving.ReverseTopSpeed).Within(0.01f));
        Assert.That(kart.Position.Z, Is.GreaterThan(0f));
    }

    [Test]
    public void Update_SteerLeftWhileDriving_TurnsLeft()
    {
        var kart = Drive(frames: 120, steer: 1);

        Assert.That(kart.Forward.X, Is.LessThan(0f)); // left of -Z is -X
    }

    [Test]
    public void Update_SteerWhileStandingStill_DoesNotTurn()
    {
        var kart = Drive(frames: 60, throttle: false, steer: 1);

        Assert.That(kart.Forward, Is.EqualTo(-Vector3.UnitZ));
    }
}
