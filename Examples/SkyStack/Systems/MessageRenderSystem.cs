using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using SkyStack.Climbing;
using SkyStack.Rendering;

namespace SkyStack.Systems;

/// <summary>
/// Messages over the tower: a blinking notice when the view starts rising or gets faster, the result of a finished
/// combo, and the game-over box. Drawn after the tower and the climber.
/// </summary>
public sealed class MessageRenderSystem(RunStats run, Scroller scroller, SkyStackGame game, RenderConsole console)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int NoticeRow = 8;
    private const int ComboRow = 14;
    private const int BoxLeft = 16;
    private const int BoxRight = Layout.TowerWidth - 17;
    private const int BoxTop = 20;
    private const int BoxBottom = 34;
    private const float BlinksPerSecond = 4f;

    private const ConsoleColor NoticeColor = ConsoleColor.Red;
    private const ConsoleColor ComboColor = ConsoleColor.Yellow;
    private const ConsoleColor BoxColor = ConsoleColor.White;
    private const ConsoleColor BoxBackground = ConsoleColor.DarkBlue;
    private const ConsoleColor TitleColor = ConsoleColor.Yellow;
    private const ConsoleColor ValueColor = ConsoleColor.Cyan;
    private const ConsoleColor HintColor = ConsoleColor.Gray;

    private float _seconds;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        _seconds += deltaSeconds;

        if (run.IsOver)
        {
            DrawGameOver();
            return;
        }

        if (scroller.NoticeSecondsLeft > 0f && IsBlinkOn)
        {
            var notice = scroller.Level == 1 ? SkyStackTexts.TowerRises : SkyStackTexts.Faster;
            console.Draw(TowerCentered(notice.Length), NoticeRow, notice, NoticeColor);
        }

        if (run.ComboResultSecondsLeft > 0f)
            DrawComboResult();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private bool IsBlinkOn => (int)(_seconds * BlinksPerSecond) % 2 == 0;

    /// <summary>"COMBO 12 FLOORS  +240", centred on the tower.</summary>
    private void DrawComboResult()
    {
        var floors = run.LastComboFloors;
        var points = run.LastComboPoints;
        var length = SkyStackTexts.ComboResult.Length + ConsoleText.DigitCount(floors) +
                     SkyStackTexts.ComboResultFloors.Length + ConsoleText.DigitCount(points);
        var x = TowerCentered(length);
        console.Draw(x, ComboRow, SkyStackTexts.ComboResult, ComboColor);
        x += SkyStackTexts.ComboResult.Length;
        x += ConsoleText.DrawNumber(console, x, ComboRow, floors, ComboColor);
        console.Draw(x, ComboRow, SkyStackTexts.ComboResultFloors, ComboColor);
        x += SkyStackTexts.ComboResultFloors.Length;
        ConsoleText.DrawNumber(console, x, ComboRow, points, ComboColor);
    }

    private void DrawGameOver()
    {
        for (var row = BoxTop; row <= BoxBottom; row++)
        for (var column = BoxLeft; column <= BoxRight; column++)
            console.Draw(column, row, BoxChar(column, row), BoxColor, BoxBackground);

        var row0 = BoxTop + 2;
        console.Draw(TowerCentered(SkyStackTexts.GameOver.Length), row0, SkyStackTexts.GameOver, TitleColor,
            BoxBackground);

        DrawStat(row0 + 2, SkyStackTexts.GameOverFloor, run.HighestFloor);
        DrawStat(row0 + 3, SkyStackTexts.GameOverScore, run.Score);
        DrawStat(row0 + 4, SkyStackTexts.GameOverBestCombo, run.BestComboFloors);

        if (run.Score > 0 && run.Score == game.BestScore && IsBlinkOn)
        {
            console.Draw(TowerCentered(SkyStackTexts.GameOverNewBest.Length), row0 + 6,
                SkyStackTexts.GameOverNewBest, TitleColor, BoxBackground);
        }

        if (run.SecondsSinceOver < RestartSystem.DelaySeconds)
            return;

        console.Draw(BoxLeft + 4, BoxBottom - 3, SkyStackTexts.GameOverRetry, HintColor, BoxBackground);
        console.Draw(BoxLeft + 4, BoxBottom - 2, SkyStackTexts.GameOverTitle, HintColor, BoxBackground);
    }

    private void DrawStat(int row, string label, int value)
    {
        console.Draw(BoxLeft + 4, row, label, BoxColor, BoxBackground);
        var x = BoxRight - 3 - ConsoleText.DigitCount(value) + 1;
        ConsoleText.DrawNumber(console, x, row, value, ValueColor, BoxBackground);
    }

    private static char BoxChar(int column, int row)
    {
        var left = column == BoxLeft;
        var right = column == BoxRight;
        var top = row == BoxTop;
        var bottom = row == BoxBottom;
        if (top) return left ? '╔' : right ? '╗' : '═';
        if (bottom) return left ? '╚' : right ? '╝' : '═';
        return left || right ? '║' : ' ';
    }

    private static int TowerCentered(int length) => ConsoleText.Centered(0, Layout.TowerWidth, length);
}
