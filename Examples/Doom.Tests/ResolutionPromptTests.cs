using System;
using NUnit.Framework;

namespace Doom.Tests;

public class ResolutionPromptTests
{
    [TestCase("320x200")]
    [TestCase("320X200")]
    public void FromArguments_HighValue_ReturnsHigh(string value) =>
        Assert.That(ResolutionPrompt.FromArguments(["--resolution", value]), Is.SameAs(Layout.High));

    [Test]
    public void FromArguments_StandardValue_ReturnsStandard() =>
        Assert.That(ResolutionPrompt.FromArguments(["--resolution", "160x100"]), Is.SameAs(Layout.Standard));

    [TestCase]
    [TestCase("--resolution")]
    [TestCase("--resolution", "640x400")]
    [TestCase("320x200")]
    public void FromArguments_MissingOrUnknown_ReturnsNull(params string[] args) =>
        Assert.That(ResolutionPrompt.FromArguments(args), Is.Null);

    [TestCase('1', ConsoleKey.D1)]
    [TestCase('1', ConsoleKey.NumPad1)]
    [TestCase('\r', ConsoleKey.Enter)]
    public void FromKey_OneOrEnter_ReturnsStandard(char keyChar, ConsoleKey key) =>
        Assert.That(ResolutionPrompt.FromKey(new ConsoleKeyInfo(keyChar, key, false, false, false)),
            Is.SameAs(Layout.Standard));

    [TestCase('2', ConsoleKey.D2)]
    [TestCase('2', ConsoleKey.NumPad2)]
    public void FromKey_Two_ReturnsHigh(char keyChar, ConsoleKey key) =>
        Assert.That(ResolutionPrompt.FromKey(new ConsoleKeyInfo(keyChar, key, false, false, false)),
            Is.SameAs(Layout.High));

    [Test]
    public void FromKey_OtherKey_ReturnsNull() =>
        Assert.That(ResolutionPrompt.FromKey(new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false)), Is.Null);
}
