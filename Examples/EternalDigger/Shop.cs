using EternalDigger.Components;
using EternalDigger.Mining;

namespace EternalDigger;

/// <summary>The house: buys the ore in the miner's bag and sells ladders.</summary>
public static class Shop
{
    public const int LadderPrice = 10;
    public const int StartLadders = 8;
    public const int StartGold = 0;

    /// <summary>Empties the bag into gold. Returns the gold earned (0 for an empty bag).</summary>
    public static int Deliver(ref Miner miner)
    {
        var earned = miner.CopperOre * Tiles.CopperValue + miner.SilverOre * Tiles.SilverValue +
                     miner.GoldOre * Tiles.GoldValue;
        miner.Gold += earned;
        miner.CopperOre = 0;
        miner.SilverOre = 0;
        miner.GoldOre = 0;
        return earned;
    }

    /// <summary>Buys one ladder for <see cref="LadderPrice"/>. False if the miner can't afford it.</summary>
    public static bool TryBuyLadder(ref Miner miner)
    {
        if (miner.Gold < LadderPrice)
            return false;

        miner.Gold -= LadderPrice;
        miner.Ladders++;
        return true;
    }

    /// <summary>Puts one piece of ore in the bag. False for tiles that aren't ore.</summary>
    public static bool Collect(ref Miner miner, TileType type)
    {
        switch (type)
        {
            case TileType.Copper: miner.CopperOre++; return true;
            case TileType.Silver: miner.SilverOre++; return true;
            case TileType.Gold: miner.GoldOre++; return true;
            default: return false;
        }
    }
}
