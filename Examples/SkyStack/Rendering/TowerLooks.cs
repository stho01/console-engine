using System;
using SkyStack.Climbing;

namespace SkyStack.Rendering;

/// <summary>
/// Colours of the tower. Every <see cref="Tower.MilestoneEvery"/> floors a new zone begins; after the last zone the
/// list starts over.
/// </summary>
public static class TowerLooks
{
    //**********************************************************
    //** types
    //**********************************************************

    public readonly record struct Zone(string Name, ConsoleColor Wall, ConsoleColor Platform, ConsoleColor Backdrop);

    //**********************************************************
    //** fields
    //**********************************************************

    public static readonly Zone[] Zones =
    [
        new("STONE", ConsoleColor.DarkGray, ConsoleColor.Gray, ConsoleColor.DarkGray),
        new("MOSS", ConsoleColor.DarkGreen, ConsoleColor.Green, ConsoleColor.DarkGreen),
        new("BRICK", ConsoleColor.DarkRed, ConsoleColor.Red, ConsoleColor.DarkRed),
        new("CRYSTAL", ConsoleColor.DarkMagenta, ConsoleColor.Magenta, ConsoleColor.DarkMagenta),
        new("SKY", ConsoleColor.DarkBlue, ConsoleColor.Cyan, ConsoleColor.DarkCyan),
        new("GOLD", ConsoleColor.DarkYellow, ConsoleColor.Yellow, ConsoleColor.DarkYellow)
    ];

    public const ConsoleColor MilestoneColor = ConsoleColor.White;
    public const ConsoleColor GroundColor = ConsoleColor.DarkGray;

    public const char WallChar = '▓';
    public const char WallSeamChar = '▒';
    public const char PlatformChar = '▀';
    public const char BackdropChar = '·';
    public const char GroundChar = '░';

    //**********************************************************
    //** public methods
    //**********************************************************

    public static ref readonly Zone ZoneOf(int floor) => ref Zones[Tower.ZoneOf(floor) % Zones.Length];

    /// <summary><paramref name="value"/> modulo <paramref name="divisor"/>, never negative.</summary>
    public static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;
}
