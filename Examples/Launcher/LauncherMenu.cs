using System;

namespace Launcher;

/// <summary>Menu selection and key handling. No console access, so it can be tested on its own.</summary>
internal sealed class LauncherMenu
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly int _count;

    //**********************************************************
    //** ctor
    //**********************************************************

    public LauncherMenu(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        _count = count;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Selected { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Up/Down (or W/S, K/J) move with wrap-around, Home/End jump, Enter/Space launch the selection, a digit 1-9
    /// selects and launches that entry, Esc/Q quit.
    /// </summary>
    public MenuAction HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.W or ConsoleKey.K:
                Selected = (Selected + _count - 1) % _count;
                return MenuAction.None;
            case ConsoleKey.DownArrow or ConsoleKey.S or ConsoleKey.J:
                Selected = (Selected + 1) % _count;
                return MenuAction.None;
            case ConsoleKey.Home:
                Selected = 0;
                return MenuAction.None;
            case ConsoleKey.End:
                Selected = _count - 1;
                return MenuAction.None;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                return MenuAction.Launch;
            case ConsoleKey.Escape or ConsoleKey.Q:
                return MenuAction.Quit;
        }

        var number = key.KeyChar - '0';
        if (number < 1 || number > Math.Min(_count, 9))
            return MenuAction.None;

        Selected = number - 1;
        return MenuAction.Launch;
    }
}
