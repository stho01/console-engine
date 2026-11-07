using EternalDigger.Components;
using EternalDigger.Mining;

namespace EternalDigger;

/// <summary>The surface trade: buys the ore in the miner's bag, sells ladders, the blasting kit and dynamite.</summary>
public static class Shop
{
    public const int LadderPrice = 10;
    public const int StartLadders = 8;
    public const int StartGold = 0;

    /// <summary>The blasting kit, bought once at the shed. Unlocks buying and planting dynamite.</summary>
    public const int BlastingKitPrice = 2500;

    /// <summary>One stick of dynamite at the shed, once the kit is owned.</summary>
    public const int DynamitePrice = 100;

    /// <summary>What the ore in the bag sells for.</summary>
    public static int BagValue(in Miner miner) =>
        miner.CopperOre * Tiles.CopperValue + miner.SilverOre * Tiles.SilverValue + miner.GoldOre * Tiles.GoldValue +
        miner.GemOre * Tiles.GemValue;

    /// <summary>Empties the bag into gold. Returns the gold earned (0 for an empty bag).</summary>
    public static int Deliver(ref Miner miner)
    {
        var earned = BagValue(miner);
        miner.Gold += earned;
        EmptyBag(ref miner);
        return earned;
    }

    /// <summary>Loses every piece of ore in the bag (a rescue).</summary>
    public static void EmptyBag(ref Miner miner)
    {
        miner.CopperOre = 0;
        miner.SilverOre = 0;
        miner.GoldOre = 0;
        miner.GemOre = 0;
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

    /// <summary>The price of what the blasting shed sells next: the kit, then sticks.</summary>
    public static int BlastingPrice(in Miner miner) => miner.HasBlastingKit ? DynamitePrice : BlastingKitPrice;

    /// <summary>Buys the blasting kit, or one stick once the kit is owned. False if the miner can't afford it.</summary>
    public static bool TryBuyBlasting(ref Miner miner)
    {
        var price = BlastingPrice(miner);
        if (miner.Gold < price)
            return false;

        miner.Gold -= price;
        if (miner.HasBlastingKit)
            miner.Dynamite++;
        else
            miner.HasBlastingKit = true;

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
            case TileType.Gem: miner.GemOre++; return true;
            default: return false;
        }
    }
}
