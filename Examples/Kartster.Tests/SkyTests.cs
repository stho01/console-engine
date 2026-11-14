using NUnit.Framework;

namespace Kartster.Tests;

public class SkyTests
{
    [Test]
    public void SkyRgb_TopRow_IsTheSkyTopColour()
    {
        Assert.That(KartsterGame.SkyRgb(0, 180), Is.EqualTo(0x598CF2u)); // (0.35, 0.55, 0.95) * 255
    }

    [Test]
    public void SkyRgb_DownTheScreen_GetsLighterWithoutSteps()
    {
        var previous = KartsterGame.SkyRgb(0, 180);
        for (var y = 1; y < 180; y++)
        {
            var rgb = KartsterGame.SkyRgb(y, 180);
            var red = (int)(rgb >> 16);
            var previousRed = (int)(previous >> 16);
            Assert.That(red - previousRed, Is.InRange(0, 1), $"row {y}");
            previous = rgb;
        }
        Assert.That(previous >> 16, Is.EqualTo(216u).Within(1)); // towards the horizon's 0.85
    }
}
