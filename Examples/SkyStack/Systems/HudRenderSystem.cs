using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using SkyStack.Climbing;
using SkyStack.Rendering;

namespace SkyStack.Systems;

/// <summary>
/// The panel right of the tower: floor, score, best score, the running combo with its timer bar, the rise speed, the
/// zone and the keys.
/// </summary>
public sealed class HudRenderSystem(RunStats run, Scroller scroller, SkyStackGame game, RenderConsole console)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int Left = Layout.HudLeft;
    private const int BarWidth = Layout.ScreenWidth - Layout.HudLeft - 2;

    private const ConsoleColor LogoColor = ConsoleColor.Cyan;
    private const ConsoleColor LabelColor = ConsoleColor.DarkGray;
    private const ConsoleColor ValueColor = ConsoleColor.White;
    private const ConsoleColor ComboColor = ConsoleColor.Yellow;
    private const ConsoleColor SpeedColor = ConsoleColor.Red;
    private const ConsoleColor KeysColor = ConsoleColor.Gray;
    private const char BarChar = '█';
    private const char EmptyBarChar = '·';

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        console.Draw(Left, 1, SkyStackTexts.Logo, LogoColor);

        DrawValue(4, SkyStackTexts.Floor, run.HighestFloor);
        DrawValue(7, SkyStackTexts.Score, run.Score);
        DrawValue(10, SkyStackTexts.Best, Math.Max(game.BestScore, run.Score));
        DrawCombo(13);
        DrawSpeed(18);

        console.Draw(Left, 22, SkyStackTexts.Zone, LabelColor);
        console.Draw(Left, 23, TowerLooks.ZoneOf(run.LandedFloor).Name, TowerLooks.ZoneOf(run.LandedFloor).Platform);

        console.Draw(Left, Layout.ScreenHeight - 5, SkyStackTexts.KeysRun, KeysColor);
        console.Draw(Left, Layout.ScreenHeight - 4, SkyStackTexts.KeysJump, KeysColor);
        console.Draw(Left, Layout.ScreenHeight - 3, SkyStackTexts.KeysTitle, KeysColor);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawValue(int row, string label, int value)
    {
        console.Draw(Left, row, label, LabelColor);
        ConsoleText.DrawNumber(console, Left, row + 1, value, ValueColor);
    }

    private void DrawCombo(int row)
    {
        console.Draw(Left, row, SkyStackTexts.Combo, LabelColor);
        if (!run.IsComboRunning)
            return;

        var x = Left;
        console.Draw(x, row + 1, SkyStackTexts.ComboJumps, ComboColor);
        x += SkyStackTexts.ComboJumps.Length;
        x += ConsoleText.DrawNumber(console, x, row + 1, run.ComboJumps, ComboColor);
        x += 2;
        x += ConsoleText.DrawNumber(console, x, row + 1, run.ComboFloors, ComboColor);
        console.Draw(x, row + 1, SkyStackTexts.ComboFloors, ComboColor);

        var filled = (int)MathF.Ceiling(run.ComboSecondsLeft / RunStats.ComboWindowSeconds * BarWidth);
        DrawBar(row + 2, filled, ComboColor);
    }

    private void DrawSpeed(int row)
    {
        console.Draw(Left, row, SkyStackTexts.Speed, LabelColor);
        if (!scroller.IsRising)
        {
            console.Draw(Left, row + 1, SkyStackTexts.SpeedWaiting, KeysColor);
            return;
        }

        for (var level = 0; level < Scroller.MaxLevel; level++)
        {
            var on = level < scroller.Level;
            console.Draw(Left + level * 2, row + 1, on ? BarChar : EmptyBarChar, on ? SpeedColor : LabelColor);
        }
    }

    private void DrawBar(int row, int filled, ConsoleColor color)
    {
        for (var i = 0; i < BarWidth; i++)
            console.Draw(Left + i, row, i < filled ? BarChar : EmptyBarChar, i < filled ? color : LabelColor);
    }
}
