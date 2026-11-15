using NUnit.Framework;
using SkyStack.Climbing;

namespace SkyStack.Tests;

public class ScrollerTests
{
    private const float Top = -Layout.GroundViewRow;

    [Test]
    public void Update_BelowTheStartFloor_DoesNotRise()
    {
        var scroller = new Scroller(Top);

        scroller.Update(10f, Tower.RowOf(0), highestFloor: Scroller.StartFloor - 1);

        Assert.That(scroller.IsRising, Is.False);
        Assert.That(scroller.Top, Is.EqualTo(Top));
    }

    [Test]
    public void Update_StartFloorReached_StartsRisingWithANotice()
    {
        var scroller = new Scroller(Top);

        scroller.Update(1f, Tower.RowOf(Scroller.StartFloor), Scroller.StartFloor);

        Assert.That(scroller.Level, Is.EqualTo(1));
        Assert.That(scroller.NoticeSecondsLeft, Is.GreaterThan(0f));
        Assert.That(scroller.Top, Is.LessThan(Top));
    }

    [Test]
    public void Update_EveryLevelSeconds_RisesFasterUpToTheMaxLevel()
    {
        var scroller = new Scroller(Top);
        scroller.Update(.1f, 0f, Scroller.StartFloor);
        var speed = scroller.Speed;

        for (var i = 0; i < Scroller.MaxLevel + 3; i++)
            scroller.Update(Scroller.LevelSeconds, 0f, Scroller.StartFloor);

        Assert.That(scroller.Level, Is.EqualTo(Scroller.MaxLevel));
        Assert.That(scroller.Speed, Is.GreaterThan(speed));
    }

    [Test]
    public void Update_ClimberNearTheTop_PullsTheViewUp()
    {
        var scroller = new Scroller(Top);
        var climberRow = Top + 2f;

        scroller.Update(.01f, climberRow, highestFloor: 0);

        Assert.That(scroller.Top, Is.EqualTo(climberRow - Scroller.CatchUpPixels));
    }

    [Test]
    public void HasLost_ClimberOnScreen_IsFalse()
    {
        var scroller = new Scroller(Top);

        Assert.That(scroller.HasLost(Tower.RowOf(0)), Is.False);
    }

    [Test]
    public void HasLost_HeadBelowTheBottomEdge_IsTrue()
    {
        var scroller = new Scroller(Top);
        var belowScreen = Top + Layout.ViewHeight + ClimbPhysics.BodyHeight;

        Assert.That(scroller.HasLost(belowScreen), Is.True);
    }
}
