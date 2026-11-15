using System;
using SkyStack.Climbing;

namespace SkyStack.Rendering;

/// <summary>
/// The climber's pixel art: a little figure with a bobble hat, <see cref="ClimbPhysics.BodyWidth"/> ×
/// <see cref="ClimbPhysics.BodyHeight"/> pixels, looking right (drawn mirrored to look left). Palette indices:
/// h hat, f face and hands, e eye, j jacket, p trousers, b boots.
/// </summary>
public static class ClimberLooks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const string Keys = "hfejpb";

    /// <summary>Index of the jacket in a palette: the part that flashes during a combo.</summary>
    public const int JacketIndex = 3;

    public static readonly PixelSprite Standing = PixelSprite.FromRows(Keys,
        "..hh..",
        ".hhhh.",
        ".fffe.",
        ".ffff.",
        ".jjjj.",
        "jjjjjj",
        "f.jj.f",
        ".pppp.",
        ".p..p.",
        "bb..bb");

    public static readonly PixelSprite RunningA = PixelSprite.FromRows(Keys,
        "..hh..",
        ".hhhh.",
        ".fffe.",
        ".ffff.",
        ".jjjj.",
        "fjjjj.",
        "..jjjf",
        ".pppp.",
        "pp..p.",
        "b...bb");

    public static readonly PixelSprite RunningB = PixelSprite.FromRows(Keys,
        "..hh..",
        ".hhhh.",
        ".fffe.",
        ".ffff.",
        ".jjjj.",
        ".jjjjf",
        "fjjj..",
        ".ppp..",
        "..pp..",
        "..bbb.");

    public static readonly PixelSprite Rising = PixelSprite.FromRows(Keys,
        "f.hh.f",
        "fhhhhf",
        "jfffej",
        ".ffff.",
        ".jjjj.",
        ".jjjj.",
        "..jj..",
        ".pppp.",
        "pp..pp",
        "b....b");

    public static readonly PixelSprite Falling = PixelSprite.FromRows(Keys,
        "..hh..",
        ".hhhh.",
        ".fffe.",
        ".ffff.",
        "fjjjjf",
        ".jjjj.",
        "..jj..",
        ".pppp.",
        ".p..p.",
        ".b..b.");

    public static readonly ConsoleColor[] Normal =
    [
        ConsoleColor.Red, ConsoleColor.Yellow, ConsoleColor.Black, ConsoleColor.Cyan, ConsoleColor.DarkBlue,
        ConsoleColor.DarkGray
    ];

    /// <summary>One palette per combo flash colour, so flashing doesn't build a palette per frame.</summary>
    public static readonly ConsoleColor[][] Combo =
    [
        WithJacket(ConsoleColor.Yellow),
        WithJacket(ConsoleColor.Magenta),
        WithJacket(ConsoleColor.Green),
        WithJacket(ConsoleColor.White)
    ];

    public static readonly ConsoleColor[] Fallen = Plain(ConsoleColor.Red);
    public static readonly ConsoleColor[] Trail = Plain(ConsoleColor.DarkBlue);

    //**********************************************************
    //** internals
    //**********************************************************

    private static ConsoleColor[] WithJacket(ConsoleColor jacket)
    {
        var palette = (ConsoleColor[])Normal.Clone();
        palette[JacketIndex] = jacket;
        return palette;
    }

    private static ConsoleColor[] Plain(ConsoleColor color)
    {
        var palette = new ConsoleColor[Keys.Length];
        Array.Fill(palette, color);
        return palette;
    }
}
