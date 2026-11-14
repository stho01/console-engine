using System;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;

namespace Starguard.Assets;

/// <summary>
/// Every picture in the game, added once to a <see cref="SpriteLibrary"/>. Entities store the ids. Each alien kind
/// has two frames with consecutive ids, so the formation animates by adding 0 or 1 to <c>Alien.FirstFrame</c>.
/// </summary>
public sealed class SpriteSheet
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>All aliens, the cannon and explosions are this wide and <see cref="UnitHeight"/> high.</summary>
    public const int UnitWidth = 5;
    public const int UnitHeight = 2;

    public const int SaucerWidth = 7;
    public const int SaucerHeight = 2;

    //**********************************************************
    //** ctor
    //**********************************************************

    public SpriteSheet(SpriteLibrary library)
    {
        Library = library;

        // Top rows: small and worth the most.
        Scout = AddFrames(ConsoleColor.Magenta,
            [" ▐█▌ ", "▀▄▀▄▀"],
            [" ▐█▌ ", "▄▀ ▀▄"]);
        Raider = AddFrames(ConsoleColor.Cyan,
            ["▄███▄", "▀▌ ▐▀"],
            ["▄███▄", " ▐▀▌ "]);
        Brute = AddFrames(ConsoleColor.Green,
            ["█▀█▀█", "▀   ▀"],
            ["█▀█▀█", " ▀ ▀ "]);

        Cannon = Add(ConsoleColor.Green, ["  ▄  ", "█████"]);
        Wreck = Add(ConsoleColor.Red, ["▀▄ ▄▀", "▄█▀█▄"]);
        Explosion = Add(ConsoleColor.Yellow, ["▀▄ ▄▀", "▄▀ ▀▄"]);
        Saucer = Add(ConsoleColor.Red, [" ▄███▄ ", "▀▀█▀█▀▀"]);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public SpriteLibrary Library { get; }

    /// <summary>First frame of each alien kind.</summary>
    public int Scout { get; }
    public int Raider { get; }
    public int Brute { get; }

    public int Cannon { get; }

    /// <summary>The cannon after a hit, until it respawns.</summary>
    public int Wreck { get; }

    public int Explosion { get; }
    public int Saucer { get; }

    //**********************************************************
    //** internals
    //**********************************************************

    private int Add(ConsoleColor color, string[] rows) => Library.Add(Sprite.FromStringArray(rows, color));

    /// <summary>Adds both frames back to back and returns the id of the first.</summary>
    private int AddFrames(ConsoleColor color, string[] first, string[] second)
    {
        var id = Add(color, first);
        Add(color, second);
        return id;
    }
}
