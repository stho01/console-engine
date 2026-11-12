using System;
using NUnit.Framework;

namespace Launcher.Tests;

public class LauncherMenuTests
{
    private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0') => new(keyChar, key, false, false, false);

    [Test]
    public void HandleKey_DownArrow_SelectsNextEntry()
    {
        var menu = new LauncherMenu(3);

        var action = menu.HandleKey(Key(ConsoleKey.DownArrow));

        Assert.That(action, Is.EqualTo(MenuAction.None));
        Assert.That(menu.Selected, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_UpArrowOnFirstEntry_WrapsToLastEntry()
    {
        var menu = new LauncherMenu(3);

        menu.HandleKey(Key(ConsoleKey.UpArrow));

        Assert.That(menu.Selected, Is.EqualTo(2));
    }

    [Test]
    public void HandleKey_DownArrowOnLastEntry_WrapsToFirstEntry()
    {
        var menu = new LauncherMenu(3);
        menu.HandleKey(Key(ConsoleKey.End));

        menu.HandleKey(Key(ConsoleKey.DownArrow));

        Assert.That(menu.Selected, Is.EqualTo(0));
    }

    [Test]
    public void HandleKey_Enter_LaunchesSelection()
    {
        var menu = new LauncherMenu(3);
        menu.HandleKey(Key(ConsoleKey.DownArrow));

        var action = menu.HandleKey(Key(ConsoleKey.Enter, '\r'));

        Assert.That(action, Is.EqualTo(MenuAction.Launch));
        Assert.That(menu.Selected, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_DigitInRange_SelectsAndLaunchesThatEntry()
    {
        var menu = new LauncherMenu(3);

        var action = menu.HandleKey(Key(ConsoleKey.D3, '3'));

        Assert.That(action, Is.EqualTo(MenuAction.Launch));
        Assert.That(menu.Selected, Is.EqualTo(2));
    }

    [TestCase(ConsoleKey.D4, '4')]
    [TestCase(ConsoleKey.D0, '0')]
    public void HandleKey_DigitOutOfRange_DoesNothing(ConsoleKey key, char keyChar)
    {
        var menu = new LauncherMenu(3);

        var action = menu.HandleKey(Key(key, keyChar));

        Assert.That(action, Is.EqualTo(MenuAction.None));
        Assert.That(menu.Selected, Is.EqualTo(0));
    }

    [TestCase(ConsoleKey.Escape)]
    [TestCase(ConsoleKey.Q)]
    public void HandleKey_QuitKey_Quits(ConsoleKey key)
    {
        var menu = new LauncherMenu(3);

        Assert.That(menu.HandleKey(Key(key)), Is.EqualTo(MenuAction.Quit));
    }

    [Test]
    public void Ctor_NoEntries_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LauncherMenu(0));
    }
}
