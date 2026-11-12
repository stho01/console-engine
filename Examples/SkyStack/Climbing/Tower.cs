using System;

namespace SkyStack.Climbing;

/// <summary>
/// The endless tower. Every floor is computed from the seed and its number, so nothing is stored and the same seed
/// always builds the same tower. The ground (floor 0) and every <see cref="MilestoneEvery"/>th floor span the whole
/// inside; the others get narrower the higher they are, down to <see cref="MinWidth"/>.
/// </summary>
public sealed class Tower(int seed)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Every this many floors there is a full-width floor, and a new zone begins.</summary>
    public const int MilestoneEvery = 50;

    /// <summary>Platform width near the ground, before the random spread.</summary>
    public const int StartWidth = 16;

    /// <summary>Platforms lose one column every this many floors.</summary>
    public const int NarrowEvery = 20;

    /// <summary>Random spread of a platform's width: up to this many columns more or fewer.</summary>
    public const int WidthSpread = 2;

    public const int MinWidth = 5;

    /// <summary>Every this many floors the floor number is written on the platform.</summary>
    public const int LabelEvery = 10;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>World row of a floor's platform.</summary>
    public static int RowOf(int floor) => -floor * Layout.FloorSpacing;

    /// <summary>The highest floor whose platform row is at or below <paramref name="row"/>.</summary>
    public static int FloorAtOrBelow(float row) => (int)MathF.Floor(-row / Layout.FloorSpacing);

    /// <summary>True for the ground and every <see cref="MilestoneEvery"/>th floor.</summary>
    public static bool IsMilestone(int floor) => floor % MilestoneEvery == 0;

    /// <summary>0 from the ground to floor 49, 1 from floor 50, and so on.</summary>
    public static int ZoneOf(int floor) => Math.Max(0, floor) / MilestoneEvery;

    public Floor GetFloor(int floor)
    {
        if (floor <= 0 || IsMilestone(floor))
            return new Floor(Layout.InnerLeft, Layout.InnerWidth);

        var hash = Hash(seed, floor);
        var spread = (int)(hash % (2 * WidthSpread + 1)) - WidthSpread;
        var width = Math.Clamp(StartWidth - floor / NarrowEvery + spread, MinWidth, Layout.InnerWidth);
        var left = Layout.InnerLeft + (int)((hash >> 8) % (uint)(Layout.InnerWidth - width + 1));
        return new Floor(left, width);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Mixes seed and floor into well-spread bits (a murmur-style finalizer).</summary>
    private static uint Hash(int seed, int floor)
    {
        var hash = unchecked((uint)seed * 0x9E3779B1u ^ (uint)floor * 0x85EBCA6Bu);
        hash ^= hash >> 16;
        hash = unchecked(hash * 0x7FEB352Du);
        hash ^= hash >> 15;
        hash = unchecked(hash * 0x846CA68Bu);
        hash ^= hash >> 16;
        return hash;
    }
}
