using System;
using Vikingferd.Components;

namespace Vikingferd.Rendering;

/// <summary>
/// How things look: colours and the pixel art (see <see cref="PixelSprite"/> for the format). A tile is 4 x 4 pixels,
/// a hero 4 x 8 (one tile wide, two high) and faces right; it is mirrored to face left. All built once.
/// </summary>
public static class Looks
{
    //**********************************************************
    //** colours
    //**********************************************************

    /// <summary>Doors, bridges, plates and levers of channel a-e.</summary>
    public static readonly ConsoleColor[] ChannelColors =
        [ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Red, ConsoleColor.DarkCyan, ConsoleColor.DarkMagenta];

    public static ConsoleColor HeroColor(ActorKind kind) => kind switch
    {
        ActorKind.Ylva => ConsoleColor.Green,
        ActorKind.Gorm => ConsoleColor.Red,
        ActorKind.Sigrid => ConsoleColor.Cyan,
        _ => ConsoleColor.DarkYellow
    };

    public const ConsoleColor RockColor = ConsoleColor.DarkGray;
    public const ConsoleColor RockLight = ConsoleColor.Gray;
    public const ConsoleColor RockDark = ConsoleColor.Black;
    public const ConsoleColor Snow = ConsoleColor.White;
    public const ConsoleColor SpikeColor = ConsoleColor.White;
    public const ConsoleColor ExitWood = ConsoleColor.DarkYellow;
    public const ConsoleColor ExitGlow = ConsoleColor.Yellow;

    //**********************************************************
    //** heroes: standing, two walking frames, in the air
    //**********************************************************

    public const int HeroWidth = 4;
    public const int HeroHeight = 8;

    private static readonly PixelSprite[] Ylva =
    [
        Hero("f77f", "7777", "6e0e", "6eee", "6aae", "aaaa", ".22.", "4..4"),
        Hero("f77f", "7777", "6e0e", "6eee", "6aae", "aaaa", ".22.", ".4.4"),
        Hero("f77f", "7777", "6e0e", "6eee", "6aae", "aaaa", ".22.", "4.4."),
        Hero("f77f", "7777", "6e0e", "6eee", "eaae", "aaaa", "222.", ".44.")
    ];

    private static readonly PixelSprite[] Gorm =
    [
        Hero("f77f", "7777", "7e0e", "cccc", "4cc4", "4444", "8888", "6..6"),
        Hero("f77f", "7777", "7e0e", "cccc", "4cc4", "4444", "8888", "66.6"),
        Hero("f77f", "7777", "7e0e", "cccc", "4cc4", "4444", "8888", "6.66"),
        Hero("f77f", "7777", "7e0e", "cccc", "ecce", "4444", "8888", ".66.")
    ];

    private static readonly PixelSprite[] Sigrid =
    [
        Hero(".33.", "3333", "3e0e", "3eee", "bbbb", "3bb3", ".33.", "7..7"),
        Hero(".33.", "3333", "3e0e", "3eee", "bbbb", "3bb3", ".33.", ".7.7"),
        Hero(".33.", "3333", "3e0e", "3eee", "bbbb", "3bb3", ".33.", "7.7."),
        Hero(".33.", "3333", "3e0e", "3eee", "bbbb", "3bb3", "333.", ".77.")
    ];

    /// <summary>Sigrid gliding: arms up, cloak spread overhead.</summary>
    private static readonly PixelSprite SigridGliding =
        Hero("bbbb", "e33e", "3e0e", "3eee", ".bb.", ".bb.", ".33.", ".77.");

    /// <summary>Hero pose: standing, walking (two frames, alternating every quarter tile), or in the air.</summary>
    public static PixelSprite HeroSprite(in Actor hero)
    {
        if (hero.Kind == ActorKind.Sigrid && hero.Gliding)
            return SigridGliding;

        var frames = hero.Kind switch
        {
            ActorKind.Ylva => Ylva,
            ActorKind.Gorm => Gorm,
            _ => Sigrid
        };

        if (!hero.OnGround)
            return frames[3];
        if (MathF.Abs(hero.Velocity.X) < .5f)
            return frames[0];
        return frames[1 + ((int)MathF.Floor(hero.Min.X * 4f) & 1)];
    }

    //**********************************************************
    //** actors and tiles (4 x 4)
    //**********************************************************

    public static readonly PixelSprite Crate = PixelSprite.Parse("6666", "e6ee", "ee6e", "6666");

    public static readonly PixelSprite Cracked = PixelSprite.Parse("6606", "6066", "0660", "6606");
    public static readonly PixelSprite DoorClosed = PixelSprite.Parse("****", "*8*8", "*8*8", "*8*8");
    public static readonly PixelSprite DoorOpen = PixelSprite.Parse("8..8", "....", "....", "8..8");
    public static readonly PixelSprite BridgeSolid = PixelSprite.Parse("*ee*", "6666", "6..6", "....");
    public static readonly PixelSprite BridgeOpen = PixelSprite.Parse("*..*", "....", "....", "....");
    public static readonly PixelSprite PlateUp = PixelSprite.Parse("....", "....", ".**.", "7**7");
    public static readonly PixelSprite PlatePressed = PixelSprite.Parse("....", "....", "....", "7**7");
    public static readonly PixelSprite LeverOff = PixelSprite.Parse("f...", ".7..", "..7.", "8**8");
    public static readonly PixelSprite LeverOn = PixelSprite.Parse("...f", "..7.", ".7..", "8**8");

    /// <summary>The cell row of a spike tile that holds the spikes (the bottom one), as glyphs.</summary>
    public const string Spikes = "▲▲▲▲";

    //**********************************************************
    //** markers
    //**********************************************************

    /// <summary>Above the hero the player controls.</summary>
    public const char ActiveMarker = '▼';

    /// <summary>Circling over a knocked-out hero.</summary>
    public const char DizzyStar = '*';

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>A stable pseudo-random number 0-255 for a position, so textures don't flicker.</summary>
    public static int Hash(int x, int y, int seed = 0)
    {
        var h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177;
        return (int)((h ^ (h >> 16)) & 255);
    }

    private static PixelSprite Hero(params string[] rows) => PixelSprite.Parse(rows);
}
