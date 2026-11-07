using System;

namespace EternalDigger.Mining;

/// <summary>The rules per <see cref="TileType"/>: solidity, toughness and ore value.</summary>
public static class Tiles
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int CopperValue = 15;
    public const int SilverValue = 40;
    public const int GoldValue = 100;

    /// <summary>Every this many metres of depth add one hit to every breakable tile.</summary>
    public const int DepthPerExtraHit = 25;

    public const int CopperDepth = 2;
    public const int SilverDepth = 12;
    public const int GoldDepth = 30;
    public const int GraniteDepth = 40;
    public const int CaveDepth = 6;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static bool IsSolid(TileType type) => type != TileType.Air;

    public static bool IsBreakable(TileType type) => type is not (TileType.Air or TileType.Grass or TileType.Bedrock);

    public static bool IsOre(TileType type) => type is TileType.Copper or TileType.Silver or TileType.Gold;

    /// <summary>Gold paid at the house for one piece of this ore, 0 for anything else.</summary>
    public static int OreValue(TileType type) => type switch
    {
        TileType.Copper => CopperValue,
        TileType.Silver => SilverValue,
        TileType.Gold => GoldValue,
        _ => 0
    };

    /// <summary>Shovel hits to break a tile at a depth in metres (<see cref="Mine.Depth"/>). 0 if unbreakable.</summary>
    public static int HitsToBreak(TileType type, int depth)
    {
        var baseHits = type switch
        {
            TileType.Dirt => 2,
            TileType.Stone => 4,
            TileType.Copper => 4,
            TileType.Silver => 6,
            TileType.Granite => 7,
            TileType.Gold => 8,
            _ => 0
        };

        return baseHits == 0 ? 0 : baseHits + Math.Max(0, depth) / DepthPerExtraHit;
    }
}
