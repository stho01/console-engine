using System;
using SkyStack.Climbing;

namespace SkyStack.Rendering;

/// <summary>
/// Colours and pixel patterns of the tower. Every <see cref="Tower.MilestoneEvery"/> floors a new zone begins; after
/// the last zone the list starts over.
/// </summary>
public static class TowerLooks
{
    //**********************************************************
    //** types
    //**********************************************************

    /// <param name="Name">Shown in the HUD.</param>
    /// <param name="Wall">Wall stones.</param>
    /// <param name="WallLight">The lit edge of each wall stone.</param>
    /// <param name="Platform">Top and middle row of a platform, and the floor number.</param>
    /// <param name="PlatformShade">Bottom row of a platform.</param>
    /// <param name="Mortar">Joints of the brick backdrop.</param>
    public readonly record struct Zone(
        string Name,
        ConsoleColor Wall,
        ConsoleColor WallLight,
        ConsoleColor Platform,
        ConsoleColor PlatformShade,
        ConsoleColor Mortar);

    //**********************************************************
    //** fields
    //**********************************************************

    public static readonly Zone[] Zones =
    [
        new("STONE", ConsoleColor.DarkGray, ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.DarkGray,
            ConsoleColor.DarkGray),
        new("MOSS", ConsoleColor.DarkGreen, ConsoleColor.Green, ConsoleColor.Green, ConsoleColor.DarkGreen,
            ConsoleColor.DarkGreen),
        new("BRICK", ConsoleColor.DarkRed, ConsoleColor.Red, ConsoleColor.Red, ConsoleColor.DarkRed,
            ConsoleColor.DarkRed),
        new("CRYSTAL", ConsoleColor.DarkMagenta, ConsoleColor.Magenta, ConsoleColor.Magenta,
            ConsoleColor.DarkMagenta, ConsoleColor.DarkMagenta),
        new("SKY", ConsoleColor.DarkBlue, ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.DarkCyan,
            ConsoleColor.DarkBlue),
        new("GOLD", ConsoleColor.DarkYellow, ConsoleColor.Yellow, ConsoleColor.Yellow, ConsoleColor.DarkYellow,
            ConsoleColor.DarkYellow)
    ];

    public const ConsoleColor Backdrop = ConsoleColor.Black;
    public const ConsoleColor MilestoneTop = ConsoleColor.White;
    public const ConsoleColor MilestoneShade = ConsoleColor.Gray;
    public const ConsoleColor Ground = ConsoleColor.DarkGray;
    public const ConsoleColor GroundSpeck = ConsoleColor.Gray;

    /// <summary>Pixel rows of one wall stone.</summary>
    public const int StoneHeight = 8;

    /// <summary>Pixel size of one backdrop brick, joints included.</summary>
    public const int BrickWidth = 12;

    public const int BrickHeight = 6;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static ref readonly Zone ZoneOf(int floor) => ref Zones[Tower.ZoneOf(floor) % Zones.Length];

    /// <summary><paramref name="value"/> modulo <paramref name="divisor"/>, never negative.</summary>
    public static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;

    /// <summary><paramref name="value"/> divided by <paramref name="divisor"/>, rounded down (also below zero).</summary>
    public static int FloorDiv(int value, int divisor) => (value - Mod(value, divisor)) / divisor;

    /// <summary>
    /// The backdrop at a world pixel: a joint of the brick wall behind the platforms, or <see cref="Backdrop"/>.
    /// Every other course of bricks is shifted by half a brick.
    /// </summary>
    public static ConsoleColor BackdropAt(int x, int worldY, in Zone zone)
    {
        if (Mod(worldY, BrickHeight) == 0)
            return zone.Mortar;

        var shift = Mod(FloorDiv(worldY, BrickHeight), 2) * BrickWidth / 2;
        return Mod(x + shift, BrickWidth) == 0 ? zone.Mortar : Backdrop;
    }

    /// <summary>A wall pixel: stones with a lit top-left edge and black joints.</summary>
    public static ConsoleColor WallAt(int columnInWall, int worldY, in Zone zone)
    {
        var rowInStone = Mod(worldY, StoneHeight);
        if (rowInStone == 0)
            return Backdrop;

        // Stones are as wide as the wall; every other stone has a vertical joint in the middle.
        var course = FloorDiv(worldY, StoneHeight);
        if (Mod(course, 2) == 1 && columnInWall == Layout.WallWidth / 2)
            return Backdrop;

        return rowInStone == 1 || columnInWall == 0 ? zone.WallLight : zone.Wall;
    }
}
