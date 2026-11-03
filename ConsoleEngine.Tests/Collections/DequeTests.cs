using System;
using ConsoleEngine.Collections;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Collections;

public class DequeTests
{
    [Test]
    public void Empty_Always_HasDefaultEndsAndZeroCount()
    {
        var deque = new Deque<int>();

        Assert.Multiple(() =>
        {
            Assert.That(deque.Count, Is.Zero);
            Assert.That(deque.Front, Is.Zero);
            Assert.That(deque.Back, Is.Zero);
            Assert.That(deque, Is.Empty);
        });
    }

    [Test]
    public void PushFrontAndPushBack_Mixed_KeepsOrderFrontToBack()
    {
        var deque = new Deque<int>();

        deque.PushBack(2);
        deque.PushFront(1);
        deque.PushBack(3);

        Assert.Multiple(() =>
        {
            Assert.That(deque, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(deque.Front, Is.EqualTo(1));
            Assert.That(deque.Back, Is.EqualTo(3));
            Assert.That(deque.Count, Is.EqualTo(3));
        });
    }

    [Test]
    public void PopFront_NonEmpty_RemovesAndReturnsFront()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        Assert.Multiple(() =>
        {
            Assert.That(deque.PopFront(), Is.EqualTo(1));
            Assert.That(deque.Front, Is.EqualTo(2));
            Assert.That(deque.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void PopBack_NonEmpty_RemovesAndReturnsBack()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        Assert.Multiple(() =>
        {
            Assert.That(deque.PopBack(), Is.EqualTo(2));
            Assert.That(deque.Back, Is.EqualTo(1));
            Assert.That(deque.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void PopFront_Empty_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Deque<int>().PopFront());
    }

    [Test]
    public void PopBack_Empty_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Deque<int>().PopBack());
    }

    [Test]
    public void PopFront_LastItem_ClearsBack()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);

        deque.PopFront();

        Assert.That(deque.Back, Is.Zero);
    }

    [Test]
    public void PopBack_LastItem_ClearsFront()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);

        deque.PopBack();

        Assert.Multiple(() =>
        {
            Assert.That(deque.Front, Is.Zero);
            Assert.That(deque, Is.Empty);
        });
    }

    [Test]
    public void PushFront_AfterPoppingLastItem_HoldsOnlyNewItem()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PopFront();

        deque.PushFront(2);

        Assert.Multiple(() =>
        {
            Assert.That(deque, Is.EqualTo(new[] { 2 }));
            Assert.That(deque.Back, Is.EqualTo(2));
        });
    }

    [Test]
    public void PushBack_AfterPoppingLastItem_HoldsOnlyNewItem()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PopBack();

        deque.PushBack(2);

        Assert.Multiple(() =>
        {
            Assert.That(deque, Is.EqualTo(new[] { 2 }));
            Assert.That(deque.Front, Is.EqualTo(2));
        });
    }
}
