using System;
using EternalDigger.Components;
using EternalDigger.Rendering;
using EternalDigger.Systems;

namespace EternalDigger;

/// <summary>The four upgrade lines sold at the shop counter, three tiers each.</summary>
public enum UpgradeLine : byte
{
    /// <summary>Digs faster.</summary>
    Shovel,
    /// <summary>Reaches further and burns less oil.</summary>
    Lamp,
    /// <summary>Carries more before the weight slows the climb.</summary>
    Bag,
    /// <summary>Ore glints from further away, and more often.</summary>
    Detector
}

/// <summary>Upgrade prices and what each tier does. The tiers live on the <see cref="Miner"/>.</summary>
public static class Upgrades
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int MaxTier = 3;

    /// <summary>Each shovel tier takes this share off <c>MinerControlSystem.DigInterval</c>.</summary>
    public const float ShovelSpeedPerTier = .15f;

    /// <summary>Each lamp tier adds this many tiles to the cone's reach.</summary>
    public const float LampReachPerTier = 2f;

    /// <summary>Each lamp tier multiplies the oil burn by this.</summary>
    public const float LampBurnPerTier = .8f;

    /// <summary>Each bag tier adds this much to the weight the bag carries before the climb slows.</summary>
    public const int BagCapacityPerTier = 10;

    /// <summary>Each detector tier adds this many tiles to the glint radius.</summary>
    public const int DetectorRadiusPerTier = 3;

    /// <summary>Each detector tier multiplies the time between glint samples by this.</summary>
    public const float DetectorIntervalPerTier = .8f;

    public const string ShovelName = "SHOVEL";
    public const string LampName = "LAMP";
    public const string BagName = "BAG";
    public const string DetectorName = "DETECTOR";

    private static readonly int[] ShovelPrices = [300, 900, 2500];
    private static readonly int[] LampPrices = [250, 750, 2000];
    private static readonly int[] BagPrices = [200, 600, 1500];
    private static readonly int[] DetectorPrices = [400, 1200, 3000];

    //**********************************************************
    //** public methods
    //**********************************************************

    public static int Tier(UpgradeLine line, in Miner miner) => line switch
    {
        UpgradeLine.Shovel => miner.ShovelTier,
        UpgradeLine.Lamp => miner.LampTier,
        UpgradeLine.Bag => miner.BagTier,
        _ => miner.DetectorTier
    };

    /// <summary>The price of the next tier, or <see cref="Shop.SoldOut"/> at <see cref="MaxTier"/>.</summary>
    public static int NextPrice(UpgradeLine line, in Miner miner)
    {
        var tier = Tier(line, miner);
        return tier >= MaxTier ? Shop.SoldOut : Prices(line)[tier];
    }

    /// <summary>Buys the next tier. False at the top tier or if the miner can't afford it.</summary>
    public static bool TryBuy(UpgradeLine line, ref Miner miner)
    {
        var price = NextPrice(line, miner);
        if (price == Shop.SoldOut || miner.Gold < price)
            return false;

        miner.Gold -= price;
        switch (line)
        {
            case UpgradeLine.Shovel: miner.ShovelTier++; break;
            case UpgradeLine.Lamp: miner.LampTier++; break;
            case UpgradeLine.Bag: miner.BagTier++; break;
            default: miner.DetectorTier++; break;
        }

        return true;
    }

    public static string Name(UpgradeLine line) => line switch
    {
        UpgradeLine.Shovel => ShovelName,
        UpgradeLine.Lamp => LampName,
        UpgradeLine.Bag => BagName,
        _ => DetectorName
    };

    /// <summary>Seconds between shovel hits at a shovel tier.</summary>
    public static float DigInterval(int shovelTier) =>
        MinerControlSystem.DigInterval * (1f - ShovelSpeedPerTier * Math.Clamp(shovelTier, 0, MaxTier));

    /// <summary>Tiles the headlamp reaches at a lamp tier, with a full tank.</summary>
    public static float LampReach(int lampTier) =>
        LightMap.LampRange + LampReachPerTier * Math.Clamp(lampTier, 0, MaxTier);

    /// <summary>Tiles around the miner whose ore can glint, at a detector tier.</summary>
    public static int GlintRadius(int detectorTier) =>
        GlintSystem.Radius + DetectorRadiusPerTier * Math.Clamp(detectorTier, 0, MaxTier);

    /// <summary>Seconds between glint samples at a detector tier.</summary>
    public static float GlintInterval(int detectorTier) =>
        GlintSystem.Interval * MathF.Pow(DetectorIntervalPerTier, Math.Clamp(detectorTier, 0, MaxTier));

    //**********************************************************
    //** internals
    //**********************************************************

    private static int[] Prices(UpgradeLine line) => line switch
    {
        UpgradeLine.Shovel => ShovelPrices,
        UpgradeLine.Lamp => LampPrices,
        UpgradeLine.Bag => BagPrices,
        _ => DetectorPrices
    };
}
