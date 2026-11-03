using System.Collections.Generic;
using ConsoleEngine.Infrastructure;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Infrastructure;

/// <summary>
/// GameTime is static and reads a real Stopwatch, so only the parts that don't depend on elapsed time are tested.
/// No test calls GameTime.Update, so GameTime.Delta stays zero here.
/// </summary>
public class GameTimeTests
{
    private readonly List<GameTime.Interval> _intervals = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var interval in _intervals)
            GameTime.ClearInterval(interval);
        _intervals.Clear();
    }

    [Test]
    public void SetInterval_Always_ReturnsUnpausedIntervalWithMs()
    {
        var interval = SetInterval(250, () => { });

        Assert.Multiple(() =>
        {
            Assert.That(interval.Ms, Is.EqualTo(250));
            Assert.That(interval.Pause, Is.False);
        });
    }

    [Test]
    public void Ms_SetNegative_ClampsToZero()
    {
        var interval = SetInterval(100, () => { });

        interval.Ms = -5;

        Assert.That(interval.Ms, Is.Zero);
    }

    [Test]
    public void UpdateInterval_ZeroMs_InvokesCallbackEveryUpdate()
    {
        var calls = 0;
        var interval = SetInterval(0, () => calls++);

        interval.UpdateInterval();
        interval.UpdateInterval();

        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void UpdateInterval_NoTimeElapsed_DoesNotInvokeCallback()
    {
        var calls = 0;
        var interval = SetInterval(100, () => calls++);

        interval.UpdateInterval();

        Assert.That(calls, Is.Zero);
    }

    private GameTime.Interval SetInterval(int ms, System.Action callback)
    {
        var interval = GameTime.SetInterval(ms, callback);
        _intervals.Add(interval);
        return interval;
    }
}
