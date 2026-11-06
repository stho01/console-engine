using ConsoleEngine.Ecs;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class ComponentMaskTests
{
    [Test]
    public void Add_IdsInEveryWord_ContainsOnlyThoseIds()
    {
        var mask = default(ComponentMask);
        mask.Add(0);
        mask.Add(63);
        mask.Add(64);
        mask.Add(255);

        Assert.Multiple(() =>
        {
            Assert.That(mask.ToIds(), Is.EqualTo(new[] { 0, 63, 64, 255 }));
            Assert.That(mask.Count, Is.EqualTo(4));
            Assert.That(mask.Contains(1), Is.False);
            Assert.That(mask.Contains(128), Is.False);
        });
    }

    [Test]
    public void Remove_PresentId_NoLongerContainsIt()
    {
        var mask = default(ComponentMask).With(70).With(3);

        mask.Remove(70);

        Assert.That(mask.ToIds(), Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void ContainsAll_Subset_ReturnsTrueOnlyForSubset()
    {
        var mask = default(ComponentMask).With(1).With(100).With(200);

        Assert.Multiple(() =>
        {
            Assert.That(mask.ContainsAll(default(ComponentMask).With(1).With(200)), Is.True);
            Assert.That(mask.ContainsAll(default(ComponentMask).With(1).With(2)), Is.False);
            Assert.That(mask.ContainsAll(default), Is.True);
        });
    }

    [Test]
    public void ContainsAny_Overlap_ReturnsTrueOnlyWhenSharingAnId()
    {
        var mask = default(ComponentMask).With(5).With(130);

        Assert.Multiple(() =>
        {
            Assert.That(mask.ContainsAny(default(ComponentMask).With(130)), Is.True);
            Assert.That(mask.ContainsAny(default(ComponentMask).With(6).With(131)), Is.False);
        });
    }

    [Test]
    public void Equals_SameIdsAddedInDifferentOrder_AreEqualWithSameHash()
    {
        var first = default(ComponentMask).With(2).With(190);
        var second = default(ComponentMask).With(190).With(2);

        Assert.Multiple(() =>
        {
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.Equals(first.With(3)), Is.False);
        });
    }
}
