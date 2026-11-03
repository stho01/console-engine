using ConsoleEngine.Extensions;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Extensions;

public class ArrayExtensionsTests
{
    [Test]
    public void To1D_TwoByThree_FlattensColumnByColumn()
    {
        var array = new[,] { { 1, 2, 3 }, { 4, 5, 6 } };

        Assert.That(array.To1D(), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
    }

    [Test]
    public void To1D_Empty_ReturnsEmptyArray()
    {
        Assert.That(new int[0, 0].To1D(), Is.Empty);
    }
}
