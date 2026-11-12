using System;
using ConsoleEngine.Ecs;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class FixedTimestepTests
{
    // A power of two, so sums of steps are exact in float.
    private const float Step = 1f / 64f;

    [Test]
    public void Advance_LessThanOneStep_ReturnsZeroAndCarriesTimeOver()
    {
        var timestep = new FixedTimestep(Step);

        var first = timestep.Advance(Step * .75f);
        var second = timestep.Advance(Step * .5f);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Zero);
            Assert.That(second, Is.EqualTo(1));
            Assert.That(timestep.Alpha, Is.EqualTo(.25f).Within(1e-5f));
        });
    }

    [Test]
    public void Advance_SeveralStepsBelowCap_ReturnsWholeStepsAndKeepsRemainder()
    {
        var timestep = new FixedTimestep(Step, maxStepsPerFrame: 4);

        var steps = timestep.Advance(Step * 3.5f);

        Assert.Multiple(() =>
        {
            Assert.That(steps, Is.EqualTo(3));
            Assert.That(timestep.Alpha, Is.EqualTo(.5f).Within(1e-5f));
        });
    }

    [Test]
    public void Advance_ExactlyCap_ReturnsCapAndKeepsRemainder()
    {
        var timestep = new FixedTimestep(Step, maxStepsPerFrame: 4);

        var steps = timestep.Advance(Step * 4.5f);

        Assert.Multiple(() =>
        {
            Assert.That(steps, Is.EqualTo(4));
            Assert.That(timestep.Alpha, Is.EqualTo(.5f).Within(1e-5f));
        });
    }

    [Test]
    public void Advance_MoreThanCap_ReturnsCapAndDropsBacklog()
    {
        var timestep = new FixedTimestep(Step, maxStepsPerFrame: 4);

        var steps = timestep.Advance(Step * 10.5f);
        var next = timestep.Advance(Step * .5f);

        Assert.Multiple(() =>
        {
            Assert.That(steps, Is.EqualTo(4));
            Assert.That(next, Is.Zero);
            Assert.That(timestep.Alpha, Is.EqualTo(.5f).Within(1e-5f));
        });
    }

    [TestCase(float.PositiveInfinity)]
    [TestCase(float.MaxValue)]
    public void Advance_HugeDelta_ReturnsCap(float delta)
    {
        var timestep = new FixedTimestep(Step, maxStepsPerFrame: 4);

        var steps = timestep.Advance(delta);

        Assert.Multiple(() =>
        {
            Assert.That(steps, Is.EqualTo(4));
            Assert.That(timestep.Alpha, Is.Zero);
        });
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    public void Advance_NotPositive_AddsNothing(float delta)
    {
        var timestep = new FixedTimestep(Step);
        timestep.Advance(Step * .5f);

        var steps = timestep.Advance(delta);

        Assert.Multiple(() =>
        {
            Assert.That(steps, Is.Zero);
            Assert.That(timestep.Alpha, Is.EqualTo(.5f).Within(1e-5f));
        });
    }

    [Test]
    public void Advance_ManySmallFrames_RunsOneStepPerStepOfTime()
    {
        var timestep = new FixedTimestep(Step);
        var total = 0;

        // 1024 frames of a 16th step each: 64 steps of time.
        for (var i = 0; i < 1024; i++)
            total += timestep.Advance(Step / 16f);

        Assert.That(total, Is.EqualTo(64));
    }

    [Test]
    public void Reset_Always_DropsCarriedTime()
    {
        var timestep = new FixedTimestep(Step);
        timestep.Advance(Step * .9f);

        timestep.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(timestep.Alpha, Is.Zero);
            Assert.That(timestep.Advance(Step * .5f), Is.Zero);
        });
    }

    [TestCase(0f)]
    [TestCase(-.1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void Ctor_InvalidStep_Throws(float stepSeconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FixedTimestep(stepSeconds));

    [TestCase(0)]
    [TestCase(-1)]
    public void Ctor_InvalidMaxSteps_Throws(int maxStepsPerFrame) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FixedTimestep(Step, maxStepsPerFrame));
}
