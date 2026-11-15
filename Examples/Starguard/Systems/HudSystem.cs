using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using Starguard.Assets.Texts;

namespace Starguard.Systems;

/// <summary>
/// Draws the score line on top, the ground line, the lives below it, and the game-over message. Runs last in the
/// presentation group. Numbers are formatted into a reused buffer, so drawing doesn't allocate.
/// </summary>
public sealed class HudSystem(StarguardGame game, Match match, Playfield playfield) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int ScoreRow = 0;
    private const ConsoleColor Label = ConsoleColor.Gray;
    private const ConsoleColor Value = ConsoleColor.White;

    private readonly char[] _text = new char[64];

    //**********************************************************
    //** props
    //**********************************************************

    private RenderConsole Console => game.Console;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        DrawScoreLine();
        Console.DrawLine(0, playfield.Ground, playfield.Width - 1, playfield.Ground, '▀', ConsoleColor.Green);
        DrawLives();

        if (match.IsOver)
            DrawGameOver();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawScoreLine()
    {
        _text.AsSpan().TryWrite($"{HudTexts.Score} {match.Score:D6}", out var length);
        DrawText(_text.AsSpan(0, length), 1, ScoreRow, Value);

        var highScore = Math.Max(game.HighScore, match.Score);
        _text.AsSpan().TryWrite($"{HudTexts.HighScore} {highScore:D6}", out length);
        DrawText(_text.AsSpan(0, length), (playfield.Width - length) / 2, ScoreRow, Label);

        _text.AsSpan().TryWrite($"{HudTexts.Wave} {match.Wave}", out length);
        DrawText(_text.AsSpan(0, length), playfield.Width - length - 1, ScoreRow, Value);
    }

    /// <summary><c>LIVES 3</c>, then one small cannon per life still in reserve.</summary>
    private void DrawLives()
    {
        var row = playfield.Ground + 1;
        _text.AsSpan().TryWrite($"{HudTexts.Lives} {match.Lives}", out var length);
        DrawText(_text.AsSpan(0, length), 1, row, Label);

        for (var life = 1; life < match.Lives; life++)
            Console.Draw(length + 1 + life * 2, row, '▲', ConsoleColor.Green);
    }

    private void DrawGameOver()
    {
        var centreRow = playfield.Height / 2;
        DrawCentred(HudTexts.GameOver, centreRow - 1, ConsoleColor.Red);
        DrawCentred(HudTexts.PlayAgain, centreRow + 1, Value);
    }

    /// <summary>Draws the text with one blank cell either side, so it stays readable over aliens and shots.</summary>
    private void DrawCentred(string text, int row, ConsoleColor color)
    {
        var column = (playfield.Width - text.Length) / 2;
        Console.Draw(column - 1, row, ' ');
        DrawText(text, column, row, color);
        Console.Draw(column + text.Length, row, ' ');
    }

    private void DrawText(ReadOnlySpan<char> text, int column, int row, ConsoleColor color)
    {
        for (var i = 0; i < text.Length; i++)
            Console.Draw(column + i, row, text[i], color);
    }
}
