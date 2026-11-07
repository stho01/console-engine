using System;
using EternalDigger.Components;
using EternalDigger.Rendering;

namespace EternalDigger;

/// <summary>
/// The headlamp's oil: a tank that burns only underground. Below <see cref="LowShare"/> the cone shortens towards
/// <see cref="LowReach"/> and flickers; empty, only the dimmed close radius is left. It never hurts the miner.
/// </summary>
public static class LampOil
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Units in a full tank.</summary>
    public const float TankSize = 100f;

    /// <summary>Seconds underground per unit burnt, with the starting lamp and a light bag.</summary>
    public const float SecondsPerUnit = 3f;

    /// <summary>Below this share of the tank the lamp is low: shorter cone, flicker, smoke.</summary>
    public const float LowShare = .3f;

    /// <summary>The cone's reach in tiles just before the tank runs dry.</summary>
    public const float LowReach = 3f;

    /// <summary>Gold per unit when filling the lamp at the counter.</summary>
    public const int PricePerUnit = 2;

    public const string FillName = "FILL LAMP";

    //**********************************************************
    //** public methods
    //**********************************************************

    public static bool IsLow(float oil) => oil < TankSize * LowShare;

    public static bool IsEmpty(float oil) => oil <= 0f;

    /// <summary>
    /// The cone's reach for a lamp tier and an oil level: the full <see cref="Upgrades.LampReach"/> while above
    /// <see cref="LowShare"/>, then down to <see cref="LowReach"/>, and 0 when empty.
    /// </summary>
    public static float Reach(int lampTier, float oil)
    {
        var full = Upgrades.LampReach(lampTier);
        if (IsEmpty(oil))
            return 0f;

        if (!IsLow(oil))
            return full;

        var share = oil / (TankSize * LowShare);
        return LowReach + (full - LowReach) * share;
    }

    /// <summary>
    /// Units burnt per second underground: the lamp tier saves oil, a <paramref name="loadFactor"/> above 1 costs more.
    /// </summary>
    public static float BurnRate(int lampTier, float loadFactor) =>
        MathF.Pow(Upgrades.LampBurnPerTier, Math.Clamp(lampTier, 0, Upgrades.MaxTier)) * loadFactor / SecondsPerUnit;

    /// <summary>Gold to fill the tank, or <see cref="Shop.SoldOut"/> when it is (all but) full.</summary>
    public static int FillPrice(in Miner miner)
    {
        var missing = (int)MathF.Floor(TankSize - miner.Oil);
        return missing <= 0 ? Shop.SoldOut : missing * PricePerUnit;
    }

    /// <summary>Fills the tank. False if it is full or the miner can't afford it.</summary>
    public static bool TryFill(ref Miner miner)
    {
        var price = FillPrice(miner);
        if (price == Shop.SoldOut || miner.Gold < price)
            return false;

        miner.Gold -= price;
        miner.Oil = TankSize;
        return true;
    }

    /// <summary>The tank in whole percent, for the counter panel.</summary>
    public static int Percent(float oil) => (int)MathF.Round(Math.Clamp(oil / TankSize, 0f, 1f) * 100f);
}
