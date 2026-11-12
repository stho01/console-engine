using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Assets.Loaders;

/// <summary>
/// Reads a <c>.sprite</c> file: an optional header of <c>!key: value</c> lines ended by a line that is exactly
/// <c>---</c>, then one row of the sprite per line, kept as written (leading and trailing spaces included). Shorter rows
/// are padded with spaces to the widest one, as <see cref="Sprite.FromStringArray"/> does.
/// <code>
/// !fg: Yellow
/// !bg: Black
/// ---
///  /\_/\
/// ( o.o )
/// </code>
/// Header keys (case-insensitive): <c>fg</c> and <c>bg</c>, each a <see cref="ConsoleColor"/> name; the defaults are
/// <c>White</c> on <c>Black</c>. A file whose first line neither starts with <c>!</c> nor is <c>---</c> has no header.
/// </summary>
public sealed class SpriteLoader : IAssetLoader<Sprite>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The line that ends the header.</summary>
    public const string HeaderEnd = "---";

    //**********************************************************
    //** ctor
    //**********************************************************

    public SpriteLoader() : this("sprite") { }

    public SpriteLoader(params string[] extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        Extensions = extensions;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public IReadOnlyList<string> Extensions { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public Sprite Load(Stream stream, AssetLoadContext context)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        var foreground = ConsoleColor.White;
        var background = ConsoleColor.Black;
        var firstRow = 0;

        if (lines.Count > 0 && (lines[0].StartsWith('!') || lines[0] == HeaderEnd))
        {
            firstRow = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i] == HeaderEnd)
                {
                    firstRow = i + 1;
                    break;
                }

                ReadHeaderLine(lines[i], i + 1, ref foreground, ref background);
            }

            if (firstRow < 0)
                throw new FormatException($"The header has no '{HeaderEnd}' line to end it.");
        }

        if (firstRow >= lines.Count)
            throw new FormatException("The sprite has no rows.");

        return Sprite.FromStringArray(lines.GetRange(firstRow, lines.Count - firstRow).ToArray(), foreground,
            background);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void ReadHeaderLine(string line, int lineNumber, ref ConsoleColor foreground,
        ref ConsoleColor background)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        var colon = line.IndexOf(':');
        if (!line.StartsWith('!') || colon < 0)
            throw new FormatException($"Line {lineNumber}: expected '!key: value' in the header, got '{line}'.");

        var key = line[1..colon].Trim();
        var value = line[(colon + 1)..].Trim();
        if (key.Equals("fg", StringComparison.OrdinalIgnoreCase))
            foreground = ParseColor(value, lineNumber);
        else if (key.Equals("bg", StringComparison.OrdinalIgnoreCase))
            background = ParseColor(value, lineNumber);
        else
            throw new FormatException($"Line {lineNumber}: unknown header key '{key}'. Known keys: fg, bg.");
    }

    private static ConsoleColor ParseColor(string value, int lineNumber) =>
        Enum.TryParse<ConsoleColor>(value, ignoreCase: true, out var color) && Enum.IsDefined(color)
            ? color
            : throw new FormatException($"Line {lineNumber}: '{value}' is not a ConsoleColor.");
}
