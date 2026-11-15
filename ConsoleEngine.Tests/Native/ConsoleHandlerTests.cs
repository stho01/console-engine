using ConsoleEngine.Native;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Native;

public class ConsoleHandlerTests
{
    private const int Width = 4;
    private const int Height = 5;

    [Test]
    public void FindChangedRows_EqualFrames_ReturnsFalse()
    {
        var shown = new uint[Width * Height];
        var next = new uint[Width * Height];

        var changed = ConsoleHandler.FindChangedRows(next, shown, Width, out _, out _);

        Assert.That(changed, Is.False);
    }

    [Test]
    public void FindChangedRows_OneCellChanged_ReturnsItsRow()
    {
        var shown = new uint[Width * Height];
        var next = new uint[Width * Height];
        next[2 * Width + 3] = 7;

        var changed = ConsoleHandler.FindChangedRows(next, shown, Width, out var top, out var bottom);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(top, Is.EqualTo(2));
            Assert.That(bottom, Is.EqualTo(2));
        });
    }

    [Test]
    public void FindChangedRows_CellsChangedInTwoRows_ReturnsTheRowsBetween()
    {
        var shown = new uint[Width * Height];
        var next = new uint[Width * Height];
        next[1 * Width] = 1;
        next[3 * Width + 1] = 1;

        ConsoleHandler.FindChangedRows(next, shown, Width, out var top, out var bottom);

        Assert.Multiple(() =>
        {
            Assert.That(top, Is.EqualTo(1));
            Assert.That(bottom, Is.EqualTo(3));
        });
    }

    [Test]
    public void FindChangedRows_FirstAndLastCellChanged_ReturnsAllRows()
    {
        var shown = new uint[Width * Height];
        var next = new uint[Width * Height];
        next[0] = 1;
        next[^1] = 1;

        ConsoleHandler.FindChangedRows(next, shown, Width, out var top, out var bottom);

        Assert.Multiple(() =>
        {
            Assert.That(top, Is.EqualTo(0));
            Assert.That(bottom, Is.EqualTo(Height - 1));
        });
    }
}
