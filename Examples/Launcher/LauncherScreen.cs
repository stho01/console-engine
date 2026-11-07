using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>Draws the menu with <see cref="System.Console"/>. Redrawn after every key, never per frame.</summary>
internal static class LauncherScreen
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int TitleColumnWidth = 20;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static void Draw(IReadOnlyList<GameEntry> games, IReadOnlyList<bool> built, int selected, string status)
    {
        Console.Clear();
        Console.WriteLine();
        WriteLine("  " + LauncherTexts.Heading, ConsoleColor.Cyan);
        WriteLine("  " + new string('─', LauncherTexts.Heading.Length), ConsoleColor.DarkCyan);
        Console.WriteLine();

        for (var i = 0; i < games.Count; i++)
            DrawEntry(i, games[i], built[i], i == selected);

        Console.WriteLine();
        WriteLine("  " + string.Format(LauncherTexts.KeyHelp, Math.Min(games.Count, 9)), ConsoleColor.DarkGray);

        if (status.Length > 0)
        {
            Console.WriteLine();
            WriteLine("  " + status, ConsoleColor.Yellow);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void DrawEntry(int index, GameEntry game, bool built, bool selected)
    {
        var label = $" {index + 1}  {game.Title.PadRight(TitleColumnWidth)}";
        Console.Write(selected ? "  ▶ " : "    ");

        if (selected)
        {
            Console.BackgroundColor = ConsoleColor.DarkCyan;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(label);
            Console.ResetColor();
        }
        else
        {
            Write(label, built ? ConsoleColor.Gray : ConsoleColor.DarkGray);
        }

        WriteLine("  " + (built ? game.Controls : LauncherTexts.NotBuiltTag), ConsoleColor.DarkGray);
    }

    private static void Write(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void WriteLine(string text, ConsoleColor color)
    {
        Write(text, color);
        Console.WriteLine();
    }
}
