using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Launcher;

/// <summary>The launcher loop: draw, read a key, start the selected game, repeat until Esc/Q.</summary>
internal sealed class LauncherApp
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly IReadOnlyList<GameEntry> _games;
    private readonly string[] _executablePaths;
    private readonly bool[] _built;
    private readonly LauncherMenu _menu;
    private string _status = string.Empty;

    //**********************************************************
    //** ctor
    //**********************************************************

    public LauncherApp(IReadOnlyList<GameEntry> games, GameLocator locator)
    {
        _games = games;
        _executablePaths = new string[games.Count];
        _built = new bool[games.Count];
        _menu = new LauncherMenu(games.Count);

        for (var i = 0; i < games.Count; i++)
            _executablePaths[i] = locator.GetExecutablePath(games[i]);
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Run()
    {
        Console.Title = LauncherTexts.WindowTitle;
        Console.CursorVisible = false;

        try
        {
            while (true)
            {
                RefreshBuilt();
                LauncherScreen.Draw(_games, _built, _menu.Selected, _status);

                var action = _menu.HandleKey(Console.ReadKey(intercept: true));
                if (action == MenuAction.Quit)
                    return;
                if (action == MenuAction.Launch)
                    Launch(_menu.Selected);
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.Clear();
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Checked on every redraw, so a game built while the launcher is open shows up without a restart.</summary>
    private void RefreshBuilt()
    {
        for (var i = 0; i < _games.Count; i++)
            _built[i] = File.Exists(_executablePaths[i]);
    }

    private void Launch(int index)
    {
        var game = _games[index];
        if (!_built[index])
        {
            _status = string.Format(LauncherTexts.NotBuilt, game.Title, _executablePaths[index]);
            return;
        }

        LauncherScreen.Draw(_games, _built, index, string.Format(LauncherTexts.Running, game.Title));

        try
        {
            GameRunner.Run(_executablePaths[index]);
            _status = string.Format(LauncherTexts.Closed, game.Title);
        }
        catch (Win32Exception e)
        {
            _status = string.Format(LauncherTexts.StartFailed, game.Title, e.Message);
        }

        DiscardBufferedKeys();
    }

    /// <summary>Keys typed into the launcher window while the game ran must not start or quit anything.</summary>
    private static void DiscardBufferedKeys()
    {
        while (Console.KeyAvailable)
            Console.ReadKey(intercept: true);
    }
}
