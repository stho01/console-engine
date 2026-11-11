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

    /// <summary>Tiles of cone reach the act 2 rock below the wall swallows.</summary>
    public const float ActTwoAbsorb = 2f;

    /// <summary>In black damp the cone keeps this share of its reach…</summary>
    public const float BlackDampReachShare = .5f;

    /// <summary>…and the lamp burns this many times the oil.</summary>
    public const float BlackDampBurn = 2f;

    /// <summary>A flask of oil carried in the pack: poured into the lamp by itself when the tank runs low.</summary>
    public const string FlaskName = "OIL FLASK";

    /// <summary>Gold for one flask at the counter.</summary>
    public const int FlaskPrice = 60;

    /// <summary>Units one flask pours into the lamp.</summary>
    public const float FlaskUnits = 40f;

    /// <summary>Flasks the pack holds.</summary>
    public const int MaxFlasks = 5;

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
    /// The cone's reach where the miner stands: <see cref="ActTwoAbsorb"/> tiles shorter below the wall (never under one
    /// tile while lit), then halved in black damp. An empty lamp stays 0.
    /// </summary>
    public static float DarkReach(float reach, bool belowTheWall, bool inBlackDamp)
    {
        if (reach <= 0f)
            return 0f;

        if (belowTheWall)
            reach = Math.Max(1f, reach - ActTwoAbsorb);

        return inBlackDamp ? Math.Max(1f, reach * BlackDampReachShare) : reach;
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

    /// <summary>Gold for one more flask, or <see cref="Shop.SoldOut"/> with <see cref="MaxFlasks"/> in the pack.</summary>
    public static int FlaskPriceFor(in Miner miner) => miner.OilFlasks >= MaxFlasks ? Shop.SoldOut : FlaskPrice;

    /// <summary>Buys a flask. False if the pack is full or the miner can't afford it.</summary>
    public static bool TryBuyFlask(ref Miner miner)
    {
        var price = FlaskPriceFor(miner);
        if (price == Shop.SoldOut || miner.Gold < price)
            return false;

        miner.Gold -= price;
        miner.OilFlasks++;
        return true;
    }

    /// <summary>
    /// Pours a carried flask into a low lamp (below <see cref="LowShare"/>), up to a full tank. Returns the units
    /// poured, 0 when the lamp isn't low or there is no flask.
    /// </summary>
    public static float TryPour(ref Miner miner)
    {
        if (miner.OilFlasks <= 0 || !IsLow(miner.Oil))
            return 0f;

        var poured = Math.Min(FlaskUnits, TankSize - miner.Oil);
        miner.Oil += poured;
        miner.OilFlasks--;
        return poured;
    }

    /// <summary>The tank in whole percent, for the counter panel.</summary>
    public static int Percent(float oil) => (int)MathF.Round(Math.Clamp(oil / TankSize, 0f, 1f) * 100f);
}
