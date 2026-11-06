using System;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;

namespace TerraForM.Ecs.Assets;

/// <summary>Every sprite an entity can show. Components can't hold a <see cref="Sprite"/>, so they store its id.</summary>
public enum SpriteId : byte
{
    StartingPoint,
    FinishPoint,
    Rock,
    Crater,
    PlantSpot,
    BonusPoint,
    BonusPointConsumed,
    RoverNorth,
    RoverSouth,
    RoverEast,
    RoverWest
}

/// <summary>The map tile and rover sprites of <c>Examples/TerraForM</c>, looked up by <see cref="SpriteId"/>.</summary>
public static class SpriteSheet
{
    //**********************************************************
    //** fields
    //**********************************************************

    private static readonly Sprite[] All =
    [
        Sprite.FromStringArray([
            "  ▒  ",
            " ▒▓▒ ",
            "▒▓S▓▒",
            " ▒▓▒ ",
            "  ▒  "
        ], ConsoleColor.Gray),
        Sprite.FromStringArray([
            "█ █ █",
            " █ █ ",
            "█ █ █",
            " █ █ ",
            "█ █ █"
        ], ConsoleColor.Green),
        Sprite.FromStringArray([
            " █▓▓ ",
            "███▒▓",
            "▓█▓▒▓",
            "▓▓░▓░",
            " ▓▓░░"
        ], ConsoleColor.Gray),
        Sprite.FromStringArray([
            " ░▓▓░",
            "▓░░░▒",
            "░░ ░░",
            "▓░░░▓",
            " ▒▓▒ "
        ], ConsoleColor.DarkGray),
        Sprite.FromStringArray([
            "    ░░    ",
            "   ░▒▒░   ",
            "  ░▒▓▓▒░  ",
            " ░▒▓▒▒▓▒░ ",
            "░▒▓▒PP▒▓▒░",
            "░▒▓▒PP▒▓▒░",
            " ░▒▓▒▒▓▒░ ",
            "  ░░▓▓▒░  ",
            "   ░▒▒░   ",
            "    ░░    "
        ], ConsoleColor.Green),
        Sprite.FromStringArray([
            "  ▒  ",
            " ▒▓▒ ",
            "▒▓B▓▒",
            " ▒▓▒ ",
            "  ▒  "
        ], ConsoleColor.Blue),
        Sprite.FromStringArray([
            "     ",
            " ▒ ▒ ",
            "▒▓▼▓▒",
            " ▒ ▒ ",
            "     "
        ], ConsoleColor.Green),
        Sprite.FromStringArray([
            @"/¯¯\",
            @"║##║",
            @" ║║ ",
            @" ║║ ",
            @"║##║",
            @"└──┘"
        ]),
        Sprite.FromStringArray([
            @"┌──┐",
            @"║##║",
            @" ║║ ",
            @" ║║ ",
            @"║##║",
            @"\__/"
        ]),
        Sprite.FromStringArray([
            @"┌═  ═\",
            @"│#══#│",
            @"│#══#│",
            @"└═  ═/"
        ]),
        Sprite.FromStringArray([
            @"/═  ═┐",
            @"│#══#│",
            @"│#══#│",
            @"\═  ═┘"
        ])
    ];

    //**********************************************************
    //** public methods
    //**********************************************************

    public static Sprite Get(SpriteId id) => All[(int)id];

    /// <summary>
    /// A library for the engine's <c>SpriteRenderSystem</c>. Sprites are added in <see cref="SpriteId"/> order, so a
    /// <c>SpriteRef.Id</c> is simply <c>(int)SpriteId</c>.
    /// </summary>
    public static SpriteLibrary CreateLibrary()
    {
        var library = new SpriteLibrary();
        for (var i = 0; i < All.Length; i++)
            library.Add(All[i]);
        return library;
    }
}
