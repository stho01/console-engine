using System;
using EternalDigger.Components;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger;

/// <summary>
/// The deep camp: a trading post at <see cref="Depth"/> m, right beside the lift's bottom landing once the lift reaches
/// that far (the lift's last segment above the wall). Bought once at the surface counter for <see cref="Price"/>
/// (<see cref="Miner.HasDeepCamp"/>); building it digs out <see cref="FirstColumn"/>–<see cref="LastColumn"/> on
/// <see cref="Row"/> and the row above, and lays a floor under it. Its counter works like the surface one: it buys
/// the bag for <see cref="SaleShare"/> of the house price and sells ladders, lamp oil, flasks and sticks at
/// <see cref="Markup"/> times the surface price.
/// </summary>
public static class DeepCamp
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Gold for the camp at the surface counter.</summary>
    public const int Price = 1500;

    /// <summary>Metres down: the lift's bottom landing just above the wall (<see cref="Tiles.WallDepth"/>).</summary>
    public const int Depth = 100;

    /// <summary>The tile row the miner trades on.</summary>
    public const int Row = Mine.SurfaceRow + Depth;

    /// <summary>The camp's columns, right of the shaft. Standing on <see cref="Row"/> there is at the counter.</summary>
    public const int FirstColumn = Elevator.Column + 1;
    public const int LastColumn = FirstColumn + 2;

    /// <summary>The share of the house price the camp pays for the bag.</summary>
    public const float SaleShare = .7f;

    /// <summary>The camp's prices are the surface prices times this, rounded up.</summary>
    public const float Markup = 1.5f;

    /// <summary>Tiles the camp lantern lights around the tent.</summary>
    public const float LanternRadius = 2.6f;

    public const string Name = "DEEP CAMP";
    public const string SellBagName = "SELL BAG";

    /// <summary>Where coins and texts at the camp come from, in world cells.</summary>
    public static readonly Vector2 EffectOrigin =
        Layout.TileCenter(FirstColumn + 1, Row) + new Vector2(0f, -6f);

    /// <summary>The lantern's light, in tile coordinates.</summary>
    public static readonly Vector2 LanternCenter = new(FirstColumn + 1.5f, Row);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Whether the miner stands at the camp's counter (only once it is built).</summary>
    public static bool IsAtCounter(in Miner miner) =>
        miner.HasDeepCamp && miner.Row == Row && miner.Column >= FirstColumn && miner.Column <= LastColumn;

    /// <summary>The camp pays <see cref="SaleShare"/> of <see cref="Shop.BagValue"/> for the bag, rounded down.</summary>
    public static int Sale(in Miner miner) => (int)(Shop.BagValue(miner) * SaleShare);

    /// <summary>A surface price with the <see cref="Markup"/>, rounded up. <see cref="Shop.SoldOut"/> stays.</summary>
    public static int MarkedUp(int price) => price == Shop.SoldOut ? Shop.SoldOut : (int)MathF.Ceiling(price * Markup);

    /// <summary>
    /// Digs the camp out: <see cref="FirstColumn"/>–<see cref="LastColumn"/> on <see cref="Row"/> and the row above
    /// become air (ore there goes into the bag, ladders stay) and every tile under them that isn't solid becomes stone.
    /// </summary>
    public static void Build(Mine mine, ref Miner miner)
    {
        for (var column = FirstColumn; column <= LastColumn; column++)
        {
            for (var row = Row - 1; row <= Row; row++)
            {
                ref var cell = ref mine.At(column, row);
                if (cell.Type == TileType.Air)
                    continue;

                Shop.Collect(ref miner, cell.Type);
                cell = new TileCell { Type = TileType.Air, HasLadder = cell.HasLadder, Seen = cell.Seen };
            }

            ref var floor = ref mine.At(column, Row + 1);
            if (!Tiles.IsSolid(floor.Type))
                floor = new TileCell { Type = TileType.Stone, Seen = floor.Seen };
        }
    }

    /// <summary>Sells the bag at the camp. Returns the gold earned (0 for an empty bag).</summary>
    public static int SellBag(ref Miner miner)
    {
        var earned = Sale(miner);
        if (miner.OreCount == 0)
            return 0;

        miner.Gold += earned;
        Progress.Earn(ref miner, earned);
        Shop.EmptyBag(ref miner);
        return earned;
    }
}
