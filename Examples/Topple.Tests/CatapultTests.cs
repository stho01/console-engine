using System;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Topple.Tests;

public class CatapultTests
{
    [Test]
    public void Turn_PastTheTop_StopsAtMaxAngle()
    {
        var catapult = new Catapult(Vector2.Zero);

        catapult.Turn(1f, 10f);

        Assert.That(catapult.Angle, Is.EqualTo(Catapult.MaxAngle));
    }

    [Test]
    public void Turn_PastTheBottom_StopsAtMinAngle()
    {
        var catapult = new Catapult(Vector2.Zero);

        catapult.Turn(-1f, 10f);

        Assert.That(catapult.Angle, Is.EqualTo(Catapult.MinAngle));
    }

    [Test]
    public void ChargeUp_LongerThanChargeTime_StopsAtFullPower()
    {
        var catapult = new Catapult(Vector2.Zero);

        catapult.ChargeUp(Catapult.ChargeSeconds * 2f);

        Assert.Multiple(() =>
        {
            Assert.That(catapult.Charge, Is.EqualTo(1f));
            Assert.That(catapult.Speed, Is.EqualTo(Catapult.MaxSpeed));
        });
    }

    [Test]
    public void Release_AfterCharging_ReturnsVelocityAlongTheArmAndResets()
    {
        var catapult = new Catapult(Vector2.Zero);
        catapult.ChargeUp(Catapult.ChargeSeconds);

        var velocity = catapult.Release(.5f);

        Assert.Multiple(() =>
        {
            Assert.That(velocity.Length(), Is.EqualTo(Catapult.MaxSpeed).Within(1e-3f));
            Assert.That(velocity.X, Is.GreaterThan(0f), "throws to the right");
            Assert.That(velocity.Y, Is.LessThan(0f), "throws upwards (screen y points down)");
            Assert.That(catapult.Charge, Is.Zero);
            Assert.That(catapult.IsCharging, Is.False);
            Assert.That(catapult.Cooldown, Is.EqualTo(.5f));
        });
    }

    [Test]
    public void PointOnArc_AtTheTop_HasRisenVSquaredOverTwoG()
    {
        var velocity = new Vector2(10f, -20f);
        var gravity = new Vector2(0f, 40f);

        var top = Catapult.PointOnArc(Vector2.Zero, velocity, gravity, seconds: .5f);

        Assert.That(top.Y, Is.EqualTo(-20f * 20f / (2f * 40f)).Within(1e-4f));
    }

    [Test]
    public void Muzzle_IsArmLengthFromPivot()
    {
        var catapult = new Catapult(new Vector2(5f, 5f));

        Assert.That(Vector2.Distance(catapult.Muzzle, catapult.Pivot), Is.EqualTo(Catapult.ArmLength).Within(1e-4f));
    }

    [Test]
    public void AngleDegrees_AtStart_IsForty() =>
        Assert.That(new Catapult(Vector2.Zero).AngleDegrees, Is.EqualTo(40));

    [Test]
    public void MaxSpeed_FiredFromTheHill_ReachesTheFarSideOfTheScreen()
    {
        var angle = 45f * MathF.PI / 180f;
        var range = Catapult.MaxSpeed * Catapult.MaxSpeed * MathF.Sin(2f * angle) / Levels.LevelSetup.Gravity;

        Assert.That(Levels.LevelBuilder.CatapultPivot.X + range, Is.GreaterThan(ToppleGame.ScreenWidth - 10));
    }
}
