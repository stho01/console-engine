using System;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class LampOilTests
{
    private const float FrameSeconds = 1f / 60f;
    private const int Seed = 9;

    [Test]
    public void Burn_ThirtySecondsUnderground_BurnsTenUnits()
    {
        var miner = new Miner { Row = Mine.SurfaceRow + 20, Oil = LampOil.TankSize };

        Play(ref miner, 30f);

        Assert.That(miner.Oil, Is.EqualTo(LampOil.TankSize - 10f).Within(.05f));
    }

    [Test]
    public void Burn_OnTheSurface_BurnsNothing()
    {
        var miner = new Miner { Row = Layout.GroundRow, Oil = 50f };

        Play(ref miner, 30f);

        Assert.That(miner.Oil, Is.EqualTo(50f));
    }

    [Test]
    public void Burn_BetterLamp_BurnsLess()
    {
        var miner = new Miner { Row = Mine.SurfaceRow + 20, Oil = LampOil.TankSize, LampTier = 2 };

        Play(ref miner, 30f);

        Assert.That(miner.Oil, Is.EqualTo(LampOil.TankSize - 10f * .64f).Within(.05f));
    }

    [Test]
    public void Burn_LongEnough_StopsAtEmpty()
    {
        var miner = new Miner { Row = Mine.SurfaceRow + 20, Oil = 1f };

        Play(ref miner, 10f);

        Assert.That(miner.Oil, Is.Zero);
    }

    [Test]
    public void Reach_ByOil_FullThenShorterThenNone()
    {
        Assert.That(LampOil.Reach(0, LampOil.TankSize), Is.EqualTo(LightMap.LampRange));
        Assert.That(LampOil.Reach(0, LampOil.TankSize * LampOil.LowShare), Is.EqualTo(LightMap.LampRange));
        Assert.That(LampOil.Reach(0, .01f), Is.EqualTo(LampOil.LowReach).Within(.01f));
        Assert.That(LampOil.Reach(0, 0f), Is.Zero);
        Assert.That(LampOil.Reach(3, LampOil.TankSize), Is.EqualTo(Upgrades.LampReach(3)));
    }

    [Test]
    public void TryFill_HalfEmpty_PaysTwoGoldPerUnit()
    {
        var miner = new Miner { Gold = 500, Oil = 40f };

        Assert.That(LampOil.FillPrice(miner), Is.EqualTo(120));
        Assert.That(Shop.TryBuy(ShopItem.LampOil, ref miner), Is.True);
        Assert.That(miner.Oil, Is.EqualTo(LampOil.TankSize));
        Assert.That(miner.Gold, Is.EqualTo(380));
        Assert.That(LampOil.FillPrice(miner), Is.EqualTo(Shop.SoldOut));
        Assert.That(Shop.TryBuy(ShopItem.LampOil, ref miner), Is.False);
    }

    [Test]
    public void TryFill_TooPoor_ChangesNothing()
    {
        var miner = new Miner { Gold = 10, Oil = 0f };

        Assert.That(LampOil.TryFill(ref miner), Is.False);
        Assert.That(miner.Oil, Is.Zero);
        Assert.That(miner.Gold, Is.EqualTo(10));
    }

    [Test]
    public void Compute_EmptyLamp_LightsOnlyTheCloseRadiusDimmer()
    {
        var mine = new Mine(Seed);
        var row = Mine.SurfaceRow + 50;
        for (var column = 1; column < 10; column++)
            mine.At(column, row) = new TileCell { Type = TileType.Air };
        var light = new LightMap(Mine.Columns, 30);
        var origin = new Vector2(2.5f, row + .5f);

        light.Compute(mine, row - 10, 20, origin, Vector2.UnitX, reach: 0f, dimmed: false);

        Assert.That(light.Get(2, row), Is.EqualTo(LightMap.Lit), "the miner's own tile, one level darker");
        Assert.That(light.Get(5, row), Is.EqualTo(LightMap.Dark), "no cone");
    }

    [Test]
    public void Compute_FlickerFrame_DimsTheLampOneLevel()
    {
        var mine = new Mine(Seed);
        var row = Mine.SurfaceRow + 50;
        for (var column = 1; column < 10; column++)
            mine.At(column, row) = new TileCell { Type = TileType.Air };
        var light = new LightMap(Mine.Columns, 30);
        var origin = new Vector2(2.5f, row + .5f);

        light.Compute(mine, row - 10, 20, origin, Vector2.UnitX, LightMap.LampRange, dimmed: true);

        Assert.That(light.Get(4, row), Is.EqualTo(LightMap.Lit));
    }

    private static void Play(ref Miner miner, float seconds)
    {
        for (var t = 0f; t < seconds; t += FrameSeconds)
            LampSystem.Burn(ref miner, FrameSeconds);
    }
}
