using System;

namespace Doom;

/// <summary>
/// Picks the <see cref="Layout"/> before the game (and its console size) exists: from <c>--resolution 160x100</c> or
/// <c>--resolution 320x200</c> on the command line, else by asking in the plain console. Input that can't be asked
/// (redirected) gets <see cref="Layout.Standard"/>.
/// </summary>
public static class ResolutionPrompt
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string ResolutionOption = "--resolution";
    public const string StandardValue = "160x100";
    public const string HighValue = "320x200";

    //**********************************************************
    //** public methods
    //**********************************************************

    public static Layout Choose(string[] args)
    {
        var fromArguments = FromArguments(args);
        if (fromArguments != null)
            return fromArguments;
        if (Console.IsInputRedirected)
            return Layout.Standard;

        Console.WriteLine(DoomTexts.ResolutionTitle);
        Console.WriteLine();
        Console.WriteLine(DoomTexts.ResolutionStandard);
        Console.WriteLine(DoomTexts.ResolutionHigh);
        Console.WriteLine(DoomTexts.ResolutionWarning1);
        Console.WriteLine(DoomTexts.ResolutionWarning2);
        Console.WriteLine();
        Console.WriteLine(DoomTexts.ResolutionHint);

        while (true)
        {
            var layout = FromKey(Console.ReadKey(intercept: true));
            if (layout != null)
                return layout;
        }
    }

    /// <summary>The layout <c>--resolution</c> names, or null when it is missing or names neither.</summary>
    public static Layout FromArguments(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], ResolutionOption, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(args[i + 1], StandardValue, StringComparison.OrdinalIgnoreCase))
                return Layout.Standard;
            if (string.Equals(args[i + 1], HighValue, StringComparison.OrdinalIgnoreCase))
                return Layout.High;
        }

        return null;
    }

    /// <summary><c>1</c> or ENTER: <see cref="Layout.Standard"/>; <c>2</c>: <see cref="Layout.High"/>; else null.</summary>
    public static Layout FromKey(ConsoleKeyInfo key) => key.Key switch
    {
        ConsoleKey.D1 or ConsoleKey.NumPad1 or ConsoleKey.Enter => Layout.Standard,
        ConsoleKey.D2 or ConsoleKey.NumPad2 => Layout.High,
        _ => key.KeyChar switch
        {
            '1' => Layout.Standard,
            '2' => Layout.High,
            _ => null
        }
    };
}
