using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Infrastructure.Scenery;
using SkyStack.Rendering;

namespace SkyStack.Scenes;

/// <summary>
/// The first screen: logo, a staircase of platforms in the zone colours, the keys and the best score of this session.
/// SPACE or ENTER starts a <see cref="TowerScene"/> with a new seed; <c>ESC</c> quits (in <see cref="SkyStackGame"/>).
/// </summary>
public sealed class TitleScene : Scene<SkyStackGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Title art, one string per row.</summary>
    private static readonly string[] Logo =
    [
        "█▀▀ █▄▀ ▀▄▀ █▀▀ ▀█▀ ▄▀▄ █▀▀ █▄▀",
        "▄▄█ █ █  █  ▄▄█  █  █▀█ █▄▄ █ █"
    ];

    private const int LogoRow = 8;
    private const int TaglineRow = 12;
    private const int StairsRow = 16;
    private const int StairsStep = 3;
    private const int StairWidth = 8;
    private const int PressRow = 32;
    private const int KeysRow = 35;
    private const int HintRow = 37;
    private const float BlinksPerSecond = 2f;

    private const ConsoleColor LogoColor = ConsoleColor.Cyan;
    private const ConsoleColor TextColor = ConsoleColor.White;
    private const ConsoleColor HintColor = ConsoleColor.DarkGray;
    private const ConsoleColor BestColor = ConsoleColor.Yellow;

    private float _seconds;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad() { }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        _seconds += (float)GameTime.Delta.TotalSeconds;

        var input = Input.Instance;
        if (input.GetKey(Key.SPACE).Pressed || input.GetKey(Key.ENTER).Pressed)
            Game.Scenes.Set(new TowerScene(Environment.TickCount));
    }

    public override void OnRender()
    {
        var console = Game.Console;
        for (var i = 0; i < Logo.Length; i++)
            console.Draw(Centered(Logo[i].Length), LogoRow + i, Logo[i], LogoColor);

        console.Draw(Centered(SkyStackTexts.Tagline.Length), TaglineRow, SkyStackTexts.Tagline, TextColor);

        DrawStairs(console);
        DrawBest(console);

        if ((int)(_seconds * BlinksPerSecond) % 2 == 0)
            console.Draw(Centered(SkyStackTexts.PressStart.Length), PressRow, SkyStackTexts.PressStart, TextColor);

        console.Draw(Centered(SkyStackTexts.TitleKeys.Length), KeysRow, SkyStackTexts.TitleKeys, HintColor);
        console.Draw(Centered(SkyStackTexts.TitleHint.Length), HintRow, SkyStackTexts.TitleHint, HintColor);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>One platform per zone, climbing from the bottom left to the top right.</summary>
    private static void DrawStairs(RenderConsole console)
    {
        var zones = TowerLooks.Zones;
        var stairsLeft = Centered(zones.Length * StairWidth);
        for (var i = 0; i < zones.Length; i++)
        {
            var row = StairsRow + (zones.Length - 1 - i) * StairsStep;
            for (var column = 0; column < StairWidth - 1; column++)
                console.Draw(stairsLeft + i * StairWidth + column, row, TowerLooks.PlatformChar, zones[i].Platform);
        }
    }

    private void DrawBest(RenderConsole console)
    {
        if (Game.BestScore <= 0)
            return;

        var row = PressRow - 3;
        var left = Layout.ScreenWidth / 2 - 14;
        console.Draw(left, row, SkyStackTexts.TitleBest, HintColor);
        ConsoleText.DrawNumber(console, left + SkyStackTexts.TitleBest.Length + 1, row, Game.BestScore, BestColor);
        var right = Layout.ScreenWidth / 2 + 2;
        console.Draw(right, row, SkyStackTexts.TitleBestFloor, HintColor);
        ConsoleText.DrawNumber(console, right + SkyStackTexts.TitleBestFloor.Length + 1, row, Game.BestFloor,
            BestColor);
    }

    private static int Centered(int length) => ConsoleText.Centered(0, Layout.ScreenWidth, length);
}
