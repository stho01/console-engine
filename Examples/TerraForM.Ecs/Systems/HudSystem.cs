using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using TerraForM.Assets;
using TerraForM.Assets.Texts;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs.Systems;

/// <summary>
/// Draws the HUD below the playfield and the score in the top-right corner, or only the game-over screen. Runs last.
/// Numbers are formatted into a reused buffer, so drawing doesn't allocate.
/// </summary>
public sealed class HudSystem(TerraformEcsGame game, Level level) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>First screen row of the HUD. The playfield is the rows above it.</summary>
    public const int Top = 35;

    private const int ScoreRow = 0;
    private const int WorldNameRow = 36;
    private const int PowerRow = 37;
    private const int DamageTakenRow = 39;
    private const int GeneratorsPlantedRow = 42;
    private const int RemainingSequencesRow = 44;
    private const int QueuedCommandsRow = 46;
    private const ConsoleColor Background = ConsoleColor.Black;
    private const string ScoreLabel = HudTexts.Score + ":";
    private const string ScoreRule = "____________________";
    private const string ScoreBlank = "                    ";
    private const string QueuedCommandsPrefix = HudTexts.QueuedCommands + " ";

    private readonly char[] _text = new char[256];

    //**********************************************************
    //** props
    //**********************************************************

    private RenderConsole Console => game.Console;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        if (level.GameOver)
        {
            DrawGameOver();
            return;
        }

        for (var x = 0; x < Console.Width; x++)
        for (var y = Top; y < Console.Height; y++)
            Console.Draw(x, y, ' ', backgroundColor: Background);

        ref readonly var rover = ref World.Get<Rover>(level.Rover);
        DrawWorldName();
        DrawGeneratorsPlanted(rover);
        DrawPower(rover);
        DrawDamageTaken(rover);
        DrawRemainingSequences(rover);
        DrawQueuedCommands();
        DrawScore();

        Console.DrawLine(0, Top, Console.Width, Top, '╍');
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawGameOver()
    {
        var sprite = Sprites.GameOver;
        Console.Draw(
            (int)Console.ScreenCenter.X - sprite.Width / 2,
            (int)Console.ScreenCenter.Y - sprite.Height / 2,
            sprite);
    }

    private void DrawWorldName()
    {
        _text.AsSpan().TryWrite($"{HudTexts.MapPrefix}:  {level.Name}", out var length);
        DrawText(_text.AsSpan(0, length), WorldNameRow, 0, ConsoleColor.DarkGreen);
    }

    private void DrawGeneratorsPlanted(in Rover rover)
    {
        _text.AsSpan().TryWrite($"{HudTexts.AtmosphereGeneratorsPlanted}: {rover.GeneratorsPlanted}", out var length);
        DrawText(_text.AsSpan(0, length), GeneratorsPlantedRow, 1, ConsoleColor.Blue);
    }

    private void DrawPower(in Rover rover)
    {
        if (rover.PowerDepleted)
        {
            DrawText(HudTexts.NoPower, PowerRow, 1, ConsoleColor.Red, ConsoleColor.White);
            return;
        }

        _text.AsSpan().TryWrite($"{HudTexts.Power}: {rover.RemainingPower:N} {HudTexts.PowerUnit}", out var length);
        DrawText(_text.AsSpan(0, length), PowerRow, 1, GetPowerColor(rover));
    }

    private void DrawDamageTaken(in Rover rover)
    {
        _text.AsSpan().TryWrite($"{HudTexts.DamageTaken}: {rover.DamageTaken}", out var length);
        DrawText(_text.AsSpan(0, length), DamageTakenRow, 1, GetDamageTakenColor(rover));
    }

    private void DrawRemainingSequences(in Rover rover)
    {
        _text.AsSpan().TryWrite($"{HudTexts.RemainingCommandSequences}: {rover.RemainingSequences}", out var length);
        DrawText(_text.AsSpan(0, length), RemainingSequencesRow, 1, ConsoleColor.Blue);
    }

    /// <summary><c>QUEUED COMMANDS</c>, then the symbols of the commands typed since the last commit.</summary>
    private void DrawQueuedCommands()
    {
        var pending = level.CommandQueue.Pending;
        if (pending.Length == 0)
            return;

        DrawText(QueuedCommandsPrefix, QueuedCommandsRow, 0, ConsoleColor.Magenta);
        var x = QueuedCommandsPrefix.Length;
        for (var i = 0; i < pending.Length; i++)
        {
            if (i > 0)
                Console.Draw(x++, QueuedCommandsRow, ' ', ConsoleColor.Magenta, Background);
            Console.Draw(x++, QueuedCommandsRow, pending[i].Symbol, ConsoleColor.Magenta, Background);
        }
    }

    private void DrawScore()
    {
        game.Score.TryFormat(_text, out var length);
        var value = _text.AsSpan(0, length);
        var left = Console.Width - (ScoreLabel.Length + value.Length);

        DrawText(ScoreRule, ScoreRow, left, ConsoleColor.DarkGray);
        DrawText(ScoreLabel, ScoreRow + 1, left, ConsoleColor.DarkGray);
        DrawText(value, ScoreRow + 1, Console.Width - value.Length, ConsoleColor.DarkGreen);
        DrawText(ScoreRule, ScoreRow + 2, left, ConsoleColor.DarkGray);
        DrawText(ScoreBlank, ScoreRow + 3, left, ConsoleColor.DarkGray);
    }

    private void DrawText(ReadOnlySpan<char> text, int row, int column, ConsoleColor foreground,
        ConsoleColor background = Background)
    {
        for (var i = 0; i < text.Length; i++)
            Console.Draw(column + i, row, text[i], foreground, background);
    }

    private static ConsoleColor GetPowerColor(in Rover rover)
    {
        if (rover.RemainingPower >= rover.MaxPower / 4 * 3) return ConsoleColor.Green;
        if (rover.RemainingPower >= rover.MaxPower / 4 * 2) return ConsoleColor.Yellow;
        return ConsoleColor.Red;
    }

    private static ConsoleColor GetDamageTakenColor(in Rover rover)
    {
        if (rover.DamageTaken > 75) return ConsoleColor.Red;
        if (rover.DamageTaken > 50) return ConsoleColor.Yellow;
        if (rover.DamageTaken > 25) return ConsoleColor.DarkGreen;
        return ConsoleColor.Green;
    }
}
