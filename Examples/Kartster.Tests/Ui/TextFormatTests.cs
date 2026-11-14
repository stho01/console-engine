using System;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

public class TextFormatTests
{
    private readonly char[] _buffer = new char[32];

    private string Written(int length) => new(_buffer, 0, length);

    [TestCase(0, "0")]
    [TestCase(42, "42")]
    [TestCase(-7, "-7")]
    [TestCase(int.MinValue, "-2147483648")]
    public void Int_WritesDigits(int value, string expected)
        => Assert.That(Written(TextFormat.Int(_buffer, value)), Is.EqualTo(expected));

    [TestCase(0f, "0:00.0")]
    [TestCase(5f, "0:05.0")]
    [TestCase(83.46f, "1:23.4")]
    [TestCase(59.99f, "0:59.9")]
    [TestCase(655.15f, "10:55.1")]
    [TestCase(-3f, "0:00.0")]
    public void Time_WritesMinutesSecondsTenths(float seconds, string expected)
        => Assert.That(Written(TextFormat.Time(_buffer, seconds)), Is.EqualTo(expected));

    [Test]
    public void Gap_PrefixesPlus() => Assert.That(Written(TextFormat.Gap(_buffer, 508f)), Is.EqualTo("+8:28.0"));

    [Test]
    public void Ratio_WritesValueSlashTotal()
        => Assert.That(Written(TextFormat.Ratio(_buffer, 2, 3)), Is.EqualTo("2/3"));

    [Test]
    public void Int_BufferTooShort_Throws()
        => Assert.Throws<ArgumentException>(() => TextFormat.Int(new char[2], 1234));

    [Test]
    public void Formatting_DoesNotAllocate()
    {
        TextFormat.Time(_buffer, 1f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        TextFormat.Time(_buffer, 83.4f);
        TextFormat.Gap(_buffer, 12f);
        TextFormat.Ratio(_buffer, 1, 3);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
