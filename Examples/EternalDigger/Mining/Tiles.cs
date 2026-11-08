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
    public const int GemValue = 250;
    public const int FossilValue = 300;
    public const int RelicValue = 750;

    /// <summary>Fossils appear from this depth down to the wall.</summary>
    public const int FossilDepth = 15;

    /// <summary>Share of act 1 tiles (from <see cref="FossilDepth"/>) with a fossil, and of act 2 tiles a relic.</summary>
    public const float FossilChance = .0015f;
    public const float RelicChance = .001f;

    /// <summary>Every this many metres of depth add one hit to every breakable tile.</summary>
    public const int DepthPerExtraHit = 25;

    public const int CopperDepth = 2;
    public const int SilverDepth = 12;
    public const int GoldDepth = 30;
    public const int GraniteDepth = 40;
    public const int CaveDepth = 6;

    /// <summary>Gas pockets fill caves from this depth down (act 1's deep caves and act 2).</summary>
    public const int GasDepth = 50;

    /// <summary>Share of 2 x 2 cave areas from <see cref="GasDepth"/> whose air tiles hold gas.</summary>
    public const float GasChance = .3f;

    /// <summary>Depth of the top of the obsidian wall, the end of act 1.</summary>
    public const int WallDepth = 110;

    /// <summary>The wall is this many tiles thick, varying per column.</summary>
    public const int WallMinThickness = 3;
    public const int WallMaxThickness = 5;

    /// <summary>From this depth, gems below the wall glint on screen from further away: a hint of what lies below.</summary>
    public const int HintDepth = 95;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static bool IsSolid(TileType type) => type != TileType.Air;

    /// <summary>The shovel can break it. Obsidian can't, though a blast can (<see cref="IsBlastOnly"/>).</summary>
    public static bool IsBreakable(TileType type) =>
        type is not (TileType.Air or TileType.Grass or TileType.Bedrock or TileType.Obsidian);

    /// <summary>Only a blast clears it: the shovel just clanks.</summary>
    public static bool IsBlastOnly(TileType type) => type == TileType.Obsidian;

    /// <summary>A single rare tile that goes into the bag and sells high: a fossil or a relic.</summary>
    public static bool IsRareFind(TileType type) => type is TileType.Fossil or TileType.Relic;

    public static bool IsOre(TileType type) =>
        type is TileType.Copper or TileType.Silver or TileType.Gold or TileType.Gem;

    /// <summary>Gold paid at the house for one piece of this ore or rare find, 0 for anything else.</summary>
    public static int OreValue(TileType type) => type switch
    {
        TileType.Copper => CopperValue,
        TileType.Silver => SilverValue,
        TileType.Gold => GoldValue,
        TileType.Gem => GemValue,
        TileType.Fossil => FossilValue,
        TileType.Relic => RelicValue,
        _ => 0
    };

    /// <summary>
    /// Shovel hits to break a tile at a depth in metres (<see cref="Mine.Depth"/>), <paramref name="fewerHits"/> fewer
    /// with a better pickaxe (<see cref="Upgrades.FewerHits"/>) but never under 1. 0 if unbreakable.
    /// </summary>
    public static int HitsToBreak(TileType type, int depth, int fewerHits = 0)
    {
        var baseHits = type switch
        {
            TileType.Dirt => 2,
            TileType.Stone => 4,
            TileType.Copper => 4,
            TileType.Silver => 6,
            TileType.Granite => 7,
            TileType.Gold => 8,
            TileType.Slate => 6,
            TileType.Quartzite => 8,
            TileType.Gem => 9,
            TileType.Fossil => 5,
            TileType.Relic => 7,
            _ => 0
        };

        return baseHits == 0 ? 0 : Math.Max(1, baseHits + Math.Max(0, depth) / DepthPerExtraHit - fewerHits);
    }
}
