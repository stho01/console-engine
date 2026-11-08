using System;
using EternalDigger.Components;
using EternalDigger.Mining;

namespace EternalDigger;

/// <summary>
/// What the surface shows of how far the miner has come: the depth record on the mine entrance, the house growing at
/// gold milestones (lifetime earnings, <see cref="Miner.GoldEarned"/>), the trophy case of rare finds, and the tool
/// shop that opens once the miner has been below the wall. All of it is kept on the <see cref="Miner"/> and saved.
/// </summary>
public static class Progress
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Gold earned in all for a flag on the roof (house level 1).</summary>
    public const int FlagGold = 2000;

    /// <summary>Gold earned in all for lit windows and a porch lamp (house level 2).</summary>
    public const int LitWindowsGold = 8000;

    /// <summary>Gold earned in all for a second floor (house level 3).</summary>
    public const int SecondFloorGold = 20000;

    public const int MaxHouseLevel = 3;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The house's level, 0 to <see cref="MaxHouseLevel"/>, for the gold earned so far.</summary>
    public static int HouseLevel(int goldEarned) =>
        goldEarned >= SecondFloorGold ? 3 : goldEarned >= LitWindowsGold ? 2 : goldEarned >= FlagGold ? 1 : 0;

    /// <summary>The gold still to earn for the next house level, 0 at the top.</summary>
    public static int GoldToNextLevel(int goldEarned) => HouseLevel(goldEarned) switch
    {
        0 => FlagGold - goldEarned,
        1 => LitWindowsGold - goldEarned,
        2 => SecondFloorGold - goldEarned,
        _ => 0
    };

    /// <summary>The tool shop opens once the miner has stood below the wall.</summary>
    public static bool IsToolShopOpen(in Miner miner) => miner.PassedTheWall;

    /// <summary>
    /// Keeps the depth record and notes the first time below the wall. Cheap enough for every frame. Returns true in
    /// the frame the miner first gets below the wall (the tool shop opens).
    /// </summary>
    public static bool Track(ref Miner miner, Mine mine)
    {
        miner.DepthRecord = Math.Max(miner.DepthRecord, Mine.Depth(miner.Row));
        if (miner.PassedTheWall || Mine.Depth(miner.Row) <= Tiles.WallDepth ||
            !mine.IsBelowTheWall(miner.Column, miner.Row))
            return false;

        miner.PassedTheWall = true;
        return true;
    }

    /// <summary>Adds a sale to the lifetime earnings. Returns true when it lifts the house to a new level.</summary>
    public static bool Earn(ref Miner miner, int gold)
    {
        var before = HouseLevel(miner.GoldEarned);
        miner.GoldEarned += gold;
        return HouseLevel(miner.GoldEarned) > before;
    }
}
