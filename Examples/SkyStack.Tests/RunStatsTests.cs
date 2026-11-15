using NUnit.Framework;
using SkyStack.Climbing;

namespace SkyStack.Tests;

public class RunStatsTests
{
    [Test]
    public void Land_HigherFloor_ScoresPointsPerFloor()
    {
        var run = new RunStats();

        run.Land(1);
        run.Land(2);

        Assert.That(run.HighestFloor, Is.EqualTo(2));
        Assert.That(run.Score, Is.EqualTo(2 * RunStats.PointsPerFloor));
        Assert.That(run.IsComboRunning, Is.False);
    }

    [Test]
    public void Land_LowerFloor_KeepsTheHighestFloor()
    {
        var run = new RunStats();

        run.Land(3);
        run.Land(1);

        Assert.That(run.HighestFloor, Is.EqualTo(3));
        Assert.That(run.LandedFloor, Is.EqualTo(1));
    }

    [Test]
    public void Land_TwoFloorsUp_StartsACombo()
    {
        var run = new RunStats();

        run.Land(2);

        Assert.That(run.IsComboRunning, Is.True);
        Assert.That(run.ComboJumps, Is.EqualTo(1));
        Assert.That(run.ComboFloors, Is.EqualTo(2));
        Assert.That(run.ComboSecondsLeft, Is.EqualTo(RunStats.ComboWindowSeconds));
    }

    [Test]
    public void Land_OneFloorUpAfterComboJumps_PaysTheCombo()
    {
        var run = new RunStats();
        run.Land(3);
        run.Land(6);

        run.Land(7);

        Assert.That(run.IsComboRunning, Is.False);
        Assert.That(run.LastComboFloors, Is.EqualTo(6));
        Assert.That(run.LastComboPoints, Is.EqualTo(6 * 2 * RunStats.ComboPointsFactor));
        Assert.That(run.Score, Is.EqualTo(7 * RunStats.PointsPerFloor + run.LastComboPoints));
        Assert.That(run.BestComboFloors, Is.EqualTo(6));
    }

    [Test]
    public void Land_SingleComboJumpThenSmallStep_PaysNothing()
    {
        var run = new RunStats();
        run.Land(3);

        run.Land(4);

        Assert.That(run.ComboPoints, Is.Zero);
        Assert.That(run.ComboResultSecondsLeft, Is.Zero);
    }

    [Test]
    public void Tick_ComboWindowRunsOut_EndsTheCombo()
    {
        var run = new RunStats();
        run.Land(2);
        run.Land(4);

        run.Tick(RunStats.ComboWindowSeconds + .1f);

        Assert.That(run.IsComboRunning, Is.False);
        Assert.That(run.ComboPoints, Is.EqualTo(4 * 2 * RunStats.ComboPointsFactor));
    }

    [Test]
    public void Finish_DuringACombo_PaysItAndStopsScoring()
    {
        var run = new RunStats();
        run.Land(2);
        run.Land(4);

        run.Finish();
        run.Land(10);

        Assert.That(run.IsOver, Is.True);
        Assert.That(run.ComboPoints, Is.GreaterThan(0));
        Assert.That(run.HighestFloor, Is.EqualTo(4));
    }

    [Test]
    public void Tick_AfterFinish_CountsSecondsSinceOver()
    {
        var run = new RunStats();
        run.Finish();

        run.Tick(.5f);
        run.Tick(.5f);

        Assert.That(run.SecondsSinceOver, Is.EqualTo(1f).Within(.001f));
    }
}
