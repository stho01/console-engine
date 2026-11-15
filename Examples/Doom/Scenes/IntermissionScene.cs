using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Doom.Maps;
using Doom.Play;

namespace Doom.Scenes;

/// <summary>
/// Between maps: kills, items and secrets in percent and the time. ENTER (or SPACE, E) goes on to the next map with
/// what the player carries; after the last map of the episode it shows the end text and goes back to the title.
/// </summary>
public sealed class IntermissionScene(string mapName, bool secret, LevelStats stats, int tics, PlayerLoadout loadout)
    : Scene<DoomGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int FirstRow = 14;

    private string _next;
    private string[] _lines = [];

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad()
    {
        var next = MapNames.Next(mapName, secret);
        _next = next != null && Game.Wad.Maps.Contains(next) ? next : null;
        var seconds = tics / Level.TicRate;
        _lines =
        [
            string.Format(DoomTexts.Finished, mapName),
            "",
            string.Format(DoomTexts.Kills, Percent(stats.Kills, stats.TotalKills)),
            string.Format(DoomTexts.Items, Percent(stats.Items, stats.TotalItems)),
            string.Format(DoomTexts.Secrets, Percent(stats.Secrets, stats.TotalSecrets)),
            string.Format(DoomTexts.Time, seconds / 60, seconds % 60),
            "",
            _next != null ? string.Format(DoomTexts.NextMap, _next) : DoomTexts.EpisodeDone,
            _next != null ? "" : DoomTexts.BackToTitle
        ];
    }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        var input = Input.Instance;
        if (input.GetKey(Key.ESCAPE).Pressed)
        {
            Game.Scenes.Set(new TitleScene());
            return;
        }

        if (!input.GetKey(Key.ENTER).Pressed && !input.GetKey(Key.SPACE).Pressed && !input.GetKey(Key.E).Pressed)
            return;

        if (_next != null)
            Game.Scenes.Set(new LevelScene(_next, loadout));
        else
            Game.Scenes.Set(new TitleScene());
    }

    public override void OnRender()
    {
        for (var i = 0; i < _lines.Length; i++)
        {
            var text = _lines[i];
            var color = i == 0 ? ConsoleColor.Red : i >= _lines.Length - 2 ? ConsoleColor.Yellow : ConsoleColor.White;
            Console.Draw(Math.Max(0, (Console.Width - text.Length) / 2), FirstRow + i * 2, text, color);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static int Percent(int found, int total) => total == 0 ? 100 : found * 100 / total;
}
