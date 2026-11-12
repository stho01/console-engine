using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Topple.Tests;

public class ImpactTests
{
    [Test]
    public void Jolt_FreeFall_IsZero()
    {
        var gravityStep = new Vector2(0f, .5f);

        var jolt = Impact.Jolt(new Vector2(3f, 10f), new Vector2(3f, 10.5f), gravityStep);

        Assert.That(jolt, Is.EqualTo(0f).Within(1e-5f));
    }

    [Test]
    public void Jolt_StoppedByTheGround_IsTheSpeedLostPlusGravity()
    {
        var gravityStep = new Vector2(0f, .5f);

        var jolt = Impact.Jolt(new Vector2(0f, 20f), Vector2.Zero, gravityStep);

        Assert.That(jolt, Is.EqualTo(20.5f).Within(1e-5f));
    }

    [TestCase(0f, 0f)]
    [TestCase(Impact.HarmlessSpeed, 0f)]
    [TestCase(Impact.HarmlessSpeed + 5f, 5f)]
    public void Damage_ForJolt_IsSpeedAboveHarmless(float jolt, float expected) =>
        Assert.That(Impact.Damage(jolt), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(0f, 1f)]
    [TestCase(Impact.BlastRadius / 2f, .5f)]
    [TestCase(Impact.BlastRadius, 0f)]
    [TestCase(Impact.BlastRadius * 2f, 0f)]
    public void BlastFalloff_ByDistance_FallsLinearlyToZero(float distance, float expected) =>
        Assert.That(Impact.BlastFalloff(distance), Is.EqualTo(expected).Within(1e-5f));
}
