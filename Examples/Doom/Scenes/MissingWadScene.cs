using System;
using System.Collections.Generic;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Doom.Wad;

namespace Doom.Scenes;

/// <summary>
/// Shown when no WAD was found (<paramref name="error"/> null: lists the places searched) or the WAD could not be
/// read (shows why). ESC quits.
/// </summary>
public sealed class MissingWadScene(string error) : Scene<DoomGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private List<string> _candidates = [];

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad() => _candidates = WadLocator.Candidates();

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Game.Stop();
    }

    public override void OnRender()
    {
        var console = Console;
        var row = 6;
        if (error != null)
        {
            console.Draw(4, row, DoomTexts.BadWadTitle, ConsoleColor.Red);
            console.Draw(4, row + 2, Clip(error, console.Width - 8), ConsoleColor.Gray);
            row += 5;
        }
        else
        {
            console.Draw(4, row, DoomTexts.MissingWadTitle, ConsoleColor.Red);
            row += 2;
        }

        console.Draw(4, row++, DoomTexts.MissingWadLine1, ConsoleColor.Gray);
        console.Draw(4, row++, DoomTexts.MissingWadLine2, ConsoleColor.Gray);
        console.Draw(4, row++, DoomTexts.MissingWadLine3, ConsoleColor.Gray);
        console.Draw(4, row++, DoomTexts.MissingWadLine4, ConsoleColor.Gray);
        row++;
        foreach (var candidate in _candidates)
            console.Draw(6, row++, Clip(candidate, console.Width - 10), ConsoleColor.DarkGray);

        console.Draw(4, row + 2, DoomTexts.MissingWadQuit, ConsoleColor.White);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static string Clip(string text, int width) =>
        text.Length <= width ? text : "…" + text[^(width - 1)..];
}
