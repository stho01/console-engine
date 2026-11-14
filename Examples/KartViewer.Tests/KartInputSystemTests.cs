using KartViewer.Systems;
using NUnit.Framework;

namespace KartViewer.Tests;

public class KartInputSystemTests
{
    private const float Frame = 0.01f;

    private static float Ramp(float steer, float target, int frames)
    {
        for (var i = 0; i < frames; i++)
            steer = KartInputSystem.RampSteer(steer, target, Frame);
        return steer;
    }

    [Test]
    public void RampSteer_FromCentre_ReachesFullLockIn025Seconds()
    {
        Assert.That(Ramp(0f, 1f, 20), Is.EqualTo(0.8f).Within(1e-4f));
        Assert.That(Ramp(0f, -1f, 25), Is.EqualTo(-1f).Within(1e-4f));
    }

    [Test]
    public void RampSteer_Released_IsBackAtCentreIn012Seconds()
    {
        Assert.That(Ramp(1f, 0f, 6), Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(Ramp(1f, 0f, 12), Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void RampSteer_OtherWay_ReturnsToCentreFirstThenSteersOut()
    {
        var steer = Ramp(1f, -1f, 12);
        Assert.That(steer, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(Ramp(steer, -1f, 26), Is.EqualTo(-1f).Within(1e-4f));
    }

    [Test]
    public void RampSteer_SlowFrame_RampsAtMost01Seconds()
    {
        Assert.That(KartInputSystem.RampSteer(0f, 1f, 1f), Is.EqualTo(0.4f).Within(1e-6f));
    }
}
