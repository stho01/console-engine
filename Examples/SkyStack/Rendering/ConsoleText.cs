using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace SkyStack.Rendering;

/// <summary>Text and numbers drawn from spans, so the HUD doesn't build a string every frame.</summary>
public static class ConsoleText
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Enough for any <see cref="int"/>, sign included.</summary>
    private const int MaxDigits = 11;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static void Draw(RenderConsole console, int x, int y, ReadOnlySpan<char> text, ConsoleColor foreground,
        ConsoleColor background = ConsoleColor.Black)
    {
        for (var i = 0; i < text.Length; i++)
            console.Draw(x + i, y, text[i], foreground, background);
    }

    /// <summary>Draws <paramref name="value"/> and returns how many columns it took.</summary>
    public static int DrawNumber(RenderConsole console, int x, int y, int value, ConsoleColor foreground,
        ConsoleColor background = ConsoleColor.Black)
    {
        Span<char> digits = stackalloc char[MaxDigits];
        value.TryFormat(digits, out var length);
        Draw(console, x, y, digits[..length], foreground, background);
        return length;
    }

    /// <summary>The number of characters <see cref="DrawNumber"/> draws for <paramref name="value"/>.</summary>
    public static int DigitCount(int value)
    {
        Span<char> digits = stackalloc char[MaxDigits];
        value.TryFormat(digits, out var length);
        return length;
    }

    /// <summary>Column at which <paramref name="length"/> characters are centred between two columns.</summary>
    public static int Centered(int left, int right, int length) => left + (right - left - length) / 2;
}
