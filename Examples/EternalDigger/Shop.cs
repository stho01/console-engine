using System;
using EternalDigger.Components;
using EternalDigger.Mining;

namespace EternalDigger;

/// <summary>
/// The surface trade: buys the ore in the miner's bag at the door, and sells what the counter offers
/// (<see cref="ShopItem"/>): ladders, lamp oil, oil flasks, the four <see cref="Upgrades"/>, the blasting kit and dynamite.
/// </summary>
public static class Shop
{
    public const int LadderPrice = 10;
    public const int StartLadders = 8;
    public const int StartGold = 0;

    /// <summary>The blasting kit, bought once at the shed. Unlocks buying and planting dynamite.</summary>
    public const int BlastingKitPrice = 2500;

    /// <summary>One stick of dynamite, once the kit is owned.</summary>
    public const int DynamitePrice = 100;

    public const string LadderName = "LADDER";
    public const string BlastingKitName = "BLASTING KIT";
    public const string DynamiteName = "DYNAMITE";

    /// <summary>Bag weight of one piece of each ore, and of a rare find.</summary>
    public const int CopperWeight = 1;
    public const int SilverWeight = 1;
    public const int GoldWeight = 2;
    public const int GemWeight = 2;
    public const int RareFindWeight = 3;

    /// <summary>
    /// Weight the starting bag carries before climbing slows (+<see cref="Upgrades.BagCapacityPerTier"/> per tier).
    /// </summary>
    public const int BaseBagCapacity = 20;

    /// <summary>Every this much weight over the capacity (or part of it) is one step of slowdown.</summary>
    public const int WeightStep = 10;

    /// <summary>Each weight step makes climbing up this much slower, up to <see cref="MaxClimbSlowdown"/> times.</summary>
    public const float ClimbSlowdownPerStep = .2f;
    public const float MaxClimbSlowdown = 3f;

    /// <summary>Each weight step makes the lamp burn this much more oil while climbing up.</summary>
    public const float OilPerStep = .5f;

    /// <summary>The price shown for an item that can't be bought any more (a maxed upgrade, a full tank).</summary>
    public const int SoldOut = -1;

    /// <summary>What the ore in the bag sells for.</summary>
    public static int BagValue(in Miner miner) =>
        miner.CopperOre * Tiles.CopperValue + miner.SilverOre * Tiles.SilverValue + miner.GoldOre * Tiles.GoldValue +
        miner.GemOre * Tiles.GemValue + miner.Fossils * Tiles.FossilValue + miner.Relics * Tiles.RelicValue;

    /// <summary>What the bag weighs.</summary>
    public static int BagWeight(in Miner miner) =>
        miner.CopperOre * CopperWeight + miner.SilverOre * SilverWeight + miner.GoldOre * GoldWeight +
        miner.GemOre * GemWeight + miner.RareFinds * RareFindWeight;

    /// <summary>The weight the bag carries before climbing slows, by bag tier.</summary>
    public static int BagCapacity(in Miner miner) =>
        BaseBagCapacity + Upgrades.BagCapacityPerTier * Math.Clamp(miner.BagTier, 0, Upgrades.MaxTier);

    /// <summary>Steps of slowdown: one per <see cref="WeightStep"/> (or part of it) over the capacity.</summary>
    public static int WeightSteps(in Miner miner)
    {
        var over = BagWeight(miner) - BagCapacity(miner);
        return over <= 0 ? 0 : (over + WeightStep - 1) / WeightStep;
    }

    public static bool IsHeavy(in Miner miner) => WeightSteps(miner) > 0;

    /// <summary>How much longer a tile of climbing up takes with this bag: 1 when light.</summary>
    public static float ClimbLoad(in Miner miner) =>
        Math.Min(MaxClimbSlowdown, 1f + ClimbSlowdownPerStep * WeightSteps(miner));

    /// <summary>How much more oil the lamp burns while climbing up with this bag: 1 when light.</summary>
    public static float OilLoad(in Miner miner) => 1f + OilPerStep * WeightSteps(miner);

    /// <summary>Empties the bag into gold. Returns the gold earned (0 for an empty bag).</summary>
    public static int Deliver(ref Miner miner)
    {
        var earned = BagValue(miner);
        miner.Gold += earned;
        EmptyBag(ref miner);
        return earned;
    }

    /// <summary>Loses everything in the bag (a rescue). The found counts stay.</summary>
    public static void EmptyBag(ref Miner miner)
    {
        miner.CopperOre = 0;
        miner.SilverOre = 0;
        miner.GoldOre = 0;
        miner.GemOre = 0;
        miner.Fossils = 0;
        miner.Relics = 0;
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

    /// <summary>What an item at the counter costs the miner now, or <see cref="SoldOut"/>.</summary>
    public static int Price(ShopItem item, in Miner miner) => item switch
    {
        _ when ShopCounter.IsUpgrade(item) => Upgrades.NextPrice(ShopCounter.Line(item), miner),
        ShopItem.LampOil => LampOil.FillPrice(miner),
        ShopItem.OilFlask => LampOil.FlaskPriceFor(miner),
        ShopItem.Blasting => BlastingPrice(miner),
        _ => LadderPrice
    };

    /// <summary>
    /// How many of an item the miner has, for the panel: the tier for upgrades, -1 when a count makes no sense (the
    /// kit).
    /// </summary>
    public static int Owned(ShopItem item, in Miner miner) => item switch
    {
        _ when ShopCounter.IsUpgrade(item) => Upgrades.Tier(ShopCounter.Line(item), miner),
        ShopItem.LampOil => LampOil.Percent(miner.Oil),
        ShopItem.OilFlask => miner.OilFlasks,
        ShopItem.Blasting => miner.HasBlastingKit ? miner.Dynamite : -1,
        _ => miner.Ladders
    };

    /// <summary>Buys one of the item. False if it is sold out or the miner can't afford it.</summary>
    public static bool TryBuy(ShopItem item, ref Miner miner) => item switch
    {
        _ when ShopCounter.IsUpgrade(item) => Upgrades.TryBuy(ShopCounter.Line(item), ref miner),
        ShopItem.LampOil => LampOil.TryFill(ref miner),
        ShopItem.OilFlask => LampOil.TryBuyFlask(ref miner),
        ShopItem.Blasting => TryBuyBlasting(ref miner),
        _ => TryBuyLadder(ref miner)
    };

    /// <summary>The price of the blasting slot: the kit, then sticks.</summary>
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
            case TileType.Fossil: miner.Fossils++; miner.FossilsFound++; return true;
            case TileType.Relic: miner.Relics++; miner.RelicsFound++; return true;
            default: return false;
        }
    }
}
