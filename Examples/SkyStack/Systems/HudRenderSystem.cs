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
    private const char SeparatorChar = '│';

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        DrawSeparator();
        console.Draw(Left, 2, SkyStackTexts.Logo, LogoColor);

        DrawValue(6, SkyStackTexts.Floor, run.HighestFloor);
        DrawValue(10, SkyStackTexts.Score, run.Score);
        DrawValue(14, SkyStackTexts.Best, Math.Max(game.BestScore, run.Score));
        DrawCombo(18);
        DrawSpeed(24);

        console.Draw(Left, 28, SkyStackTexts.Zone, LabelColor);
        ref readonly var zone = ref TowerLooks.ZoneOf(run.LandedFloor);
        console.Draw(Left, 29, zone.Name, zone.Platform);

        console.Draw(Left, Layout.ScreenHeight - 5, SkyStackTexts.KeysRun, KeysColor);
        console.Draw(Left, Layout.ScreenHeight - 4, SkyStackTexts.KeysJump, KeysColor);
        console.Draw(Left, Layout.ScreenHeight - 3, SkyStackTexts.KeysTitle, KeysColor);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>A thin line between the tower and the panel.</summary>
    private void DrawSeparator()
    {
        for (var row = 0; row < Layout.ScreenHeight; row++)
            console.Draw(Layout.TowerWidth + 1, row, SeparatorChar, LabelColor);
    }

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
