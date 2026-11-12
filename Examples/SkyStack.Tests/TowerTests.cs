using NUnit.Framework;
using SkyStack.Climbing;

namespace SkyStack.Tests;

public class TowerTests
{
    [Test]
    public void GetFloor_Ground_SpansTheWholeInside()
    {
        var floor = new Tower(1).GetFloor(0);

        Assert.That(floor.Left, Is.EqualTo(Layout.InnerLeft));
        Assert.That(floor.Right, Is.EqualTo(Layout.InnerRight));
    }

    [Test]
    public void GetFloor_Milestone_SpansTheWholeInside()
    {
        var floor = new Tower(1).GetFloor(Tower.MilestoneEvery * 3);

        Assert.That(floor.Width, Is.EqualTo(Layout.InnerWidth));
    }

    [Test]
    public void GetFloor_AnyFloor_StaysInsideTheWallsAndIsWideEnough()
    {
        var tower = new Tower(42);
        for (var floor = 1; floor < 2000; floor++)
        {
            var platform = tower.GetFloor(floor);
            Assert.That(platform.Left, Is.GreaterThanOrEqualTo(Layout.InnerLeft), $"floor {floor}");
            Assert.That(platform.Right, Is.LessThanOrEqualTo(Layout.InnerRight), $"floor {floor}");
            Assert.That(platform.Width, Is.GreaterThanOrEqualTo(Tower.MinWidth), $"floor {floor}");
        }
    }

    [Test]
    public void GetFloor_SameSeed_BuildsTheSameTower()
    {
        var first = new Tower(7);
        var second = new Tower(7);

        for (var floor = 0; floor < 200; floor++)
            Assert.That(second.GetFloor(floor), Is.EqualTo(first.GetFloor(floor)));
    }

    [Test]
    public void GetFloor_HigherFloors_AreNarrowerOnAverage()
    {
        var tower = new Tower(3);

        Assert.That(AverageWidth(tower, 1, 40), Is.GreaterThan(AverageWidth(tower, 201, 240)));
    }

    [TestCase(0, 0)]
    [TestCase(-1, 0)]
    [TestCase(-4, 1)]
    [TestCase(-5, 1)]
    [TestCase(-8, 2)]
    public void FloorAtOrBelow_Row_ReturnsTheFloorWhosePlatformIsAtOrUnderIt(int row, int expected) =>
        Assert.That(Tower.FloorAtOrBelow(row), Is.EqualTo(expected));

    [Test]
    public void ZoneOf_EveryMilestone_StartsANewZone()
    {
        Assert.That(Tower.ZoneOf(Tower.MilestoneEvery - 1), Is.Zero);
        Assert.That(Tower.ZoneOf(Tower.MilestoneEvery), Is.EqualTo(1));
    }

    private static double AverageWidth(Tower tower, int from, int to)
    {
        var sum = 0;
        for (var floor = from; floor <= to; floor++)
            sum += tower.GetFloor(floor).Width;
        return (double)sum / (to - from + 1);
    }
}
