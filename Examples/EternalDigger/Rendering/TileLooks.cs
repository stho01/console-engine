using System;
using EternalDigger.Mining;

namespace EternalDigger.Rendering;

/// <summary>
/// How a tile type looks. Fully lit, a tile shows its <see cref="Texture"/> in <see cref="Foreground"/> over a filled
/// <see cref="Background"/>; less light drops the fill (black background) and then the colour. Ores mix
/// <see cref="Speck"/> characters in <see cref="SpeckColor"/> into their rock, on <see cref="SpeckShare"/> of their cells.
/// </summary>
public readonly struct TileLook(
    string texture, ConsoleColor foreground, ConsoleColor background, ConsoleColor dimColor,
    char speck = ' ', ConsoleColor speckColor = ConsoleColor.Black, ConsoleColor dimSpeckColor = ConsoleColor.Black,
    float speckShare = .4f)
{
    public string Texture { get; } = texture;
    public ConsoleColor Foreground { get; } = foreground;
    public ConsoleColor Background { get; } = background;
    public ConsoleColor DimColor { get; } = dimColor;
    public char Speck { get; } = speck;
    public ConsoleColor SpeckColor { get; } = speckColor;
    public ConsoleColor DimSpeckColor { get; } = dimSpeckColor;
    public float SpeckShare { get; } = speckShare;
    public bool HasSpecks => Speck != ' ';
}

/// <summary>The <see cref="TileLook"/> per <see cref="TileType"/>.</summary>
public static class TileLooks
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string LadderRow = "╟──╢";
    public const string CrackCharacters = "/\\╳x";

    private static readonly TileLook Dirt =
        new("░▒░·▒░ ▒", ConsoleColor.DarkRed, ConsoleColor.DarkYellow, ConsoleColor.DarkYellow);

    private static readonly TileLook Stone =
        new("▓▒░▓█▒▓░", ConsoleColor.Gray, ConsoleColor.DarkGray, ConsoleColor.DarkGray);

    private static readonly TileLook Granite =
        new("▓█▒▓▓█░▓", ConsoleColor.DarkMagenta, ConsoleColor.DarkGray, ConsoleColor.DarkMagenta);

    private static readonly TileLook Copper = new("▒░▒·░▒", ConsoleColor.DarkRed, ConsoleColor.DarkYellow,
        ConsoleColor.DarkYellow, '◆', ConsoleColor.Red, ConsoleColor.DarkRed);

    private static readonly TileLook Silver = new("▓▒░▓▒░", ConsoleColor.Gray, ConsoleColor.DarkGray,
        ConsoleColor.DarkGray, '◆', ConsoleColor.White, ConsoleColor.Gray);

    private static readonly TileLook Gold = new("▓▒░▓▒░", ConsoleColor.Gray, ConsoleColor.DarkGray,
        ConsoleColor.DarkGray, '◆', ConsoleColor.Yellow, ConsoleColor.DarkYellow);

    private static readonly TileLook Bedrock =
        new("█▓█▓▓█▓█", ConsoleColor.DarkGray, ConsoleColor.Black, ConsoleColor.DarkGray);

    /// <summary>Dark glassy rock with an occasional bright glint, on a black fill so it reads apart from granite.</summary>
    private static readonly TileLook Obsidian = new("▓▓█▓▒▓█▓", ConsoleColor.DarkMagenta, ConsoleColor.Black,
        ConsoleColor.DarkMagenta, '▪', ConsoleColor.Magenta, ConsoleColor.DarkMagenta, .12f);

    private static readonly TileLook Slate =
        new("▓▒░▓▒▓░▒", ConsoleColor.Blue, ConsoleColor.DarkBlue, ConsoleColor.DarkBlue);

    private static readonly TileLook Quartzite =
        new("▒▓▒░▓▒█▓", ConsoleColor.Cyan, ConsoleColor.DarkCyan, ConsoleColor.DarkCyan);

    private static readonly TileLook Gem = new("▓▒░▓▒░", ConsoleColor.Blue, ConsoleColor.DarkBlue,
        ConsoleColor.DarkBlue, '◆', ConsoleColor.Magenta, ConsoleColor.DarkMagenta);

    private static readonly TileLook Grass =
        new("▒░▒·░▒ ░", ConsoleColor.DarkRed, ConsoleColor.DarkYellow, ConsoleColor.DarkYellow);

    //**********************************************************
    //** public methods
    //**********************************************************

    public static TileLook For(TileType type) => type switch
    {
        TileType.Stone => Stone,
        TileType.Granite => Granite,
        TileType.Copper => Copper,
        TileType.Silver => Silver,
        TileType.Gold => Gold,
        TileType.Bedrock => Bedrock,
        TileType.Grass => Grass,
        TileType.Obsidian => Obsidian,
        TileType.Slate => Slate,
        TileType.Quartzite => Quartzite,
        TileType.Gem => Gem,
        _ => Dirt
    };
}
