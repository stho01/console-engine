using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Doom.Play;

namespace Doom.Scenes;

/// <summary>
/// The first screen: the title, the maps in the WAD (↑ ↓ to pick, ENTER to start with a pistol-start loadout) and
/// the keys. ESC quits.
/// </summary>
public sealed class TitleScene : Scene<DoomGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int TitleRow = 8;
    private const int MapsRow = 14;
    private const int MapColumns = 5;
    private const int MapCellWidth = 10;

    private int _selected;
    private string _wadLine = "";

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad() => _wadLine = string.Format(DoomTexts.TitleWad, Game.Wad.WadPath);

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        var input = Input.Instance;
        var maps = Game.Wad.Maps;
        if (input.GetKey(Key.ESCAPE).Pressed)
        {
            Game.Stop();
            return;
        }

        if (maps.Count == 0)
            return;

        if (input.GetKey(Key.UP).Pressed || input.GetKey(Key.LEFT).Pressed)
            _selected = (_selected - 1 + maps.Count) % maps.Count;
        if (input.GetKey(Key.DOWN).Pressed || input.GetKey(Key.RIGHT).Pressed)
            _selected = (_selected + 1) % maps.Count;
        if (input.GetKey(Key.ENTER).Pressed || input.GetKey(Key.SPACE).Pressed)
            Game.Scenes.Set(new LevelScene(maps[_selected], new PlayerLoadout()));
    }

    public override void OnRender()
    {
        var console = Console;
        Centre(TitleRow, DoomTexts.Title, ConsoleColor.Red);
        Centre(TitleRow + 2, _wadLine, ConsoleColor.DarkGray);

        var maps = Game.Wad.Maps;
        var rows = (maps.Count + MapColumns - 1) / MapColumns;
        var left = (console.Width - MapColumns * MapCellWidth) / 2;
        for (var i = 0; i < maps.Count; i++)
        {
            var x = left + i % MapColumns * MapCellWidth;
            var y = MapsRow + i / MapColumns * 2;
            var selected = i == _selected;
            console.Draw(x, y, selected ? "> " : "  ", ConsoleColor.Yellow);
            console.Draw(x + 2, y, maps[i], selected ? ConsoleColor.Yellow : ConsoleColor.Gray);
        }

        var hintsRow = MapsRow + rows * 2 + 3;
        Centre(hintsRow, DoomTexts.TitleHint, ConsoleColor.White);
        Centre(hintsRow + 3, DoomTexts.TitleControls1, ConsoleColor.DarkGray);
        Centre(hintsRow + 4, DoomTexts.TitleControls2, ConsoleColor.DarkGray);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Centre(int row, string text, ConsoleColor color) =>
        Console.Draw(Math.Max(0, (Console.Width - text.Length) / 2), row, text, color);
}
