using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class UpgradesTests
{
    private const int Seed = 5;

    [TestCase(UpgradeLine.Shovel, 300, 900, 2500)]
    [TestCase(UpgradeLine.Lamp, 250, 750, 2000)]
    [TestCase(UpgradeLine.Bag, 200, 600, 1500)]
    [TestCase(UpgradeLine.Detector, 400, 1200, 3000)]
    public void TryBuy_ThreeTimes_PaysEachTierThenIsMaxed(UpgradeLine line, int first, int second, int third)
    {
        var miner = new Miner { Gold = first + second + third };

        Assert.That(Upgrades.NextPrice(line, miner), Is.EqualTo(first));
        Assert.That(Upgrades.TryBuy(line, ref miner), Is.True);
        Assert.That(Upgrades.NextPrice(line, miner), Is.EqualTo(second));
        Assert.That(Upgrades.TryBuy(line, ref miner), Is.True);
        Assert.That(Upgrades.TryBuy(line, ref miner), Is.True);

        Assert.That(Upgrades.Tier(line, miner), Is.EqualTo(Upgrades.MaxTier));
        Assert.That(miner.Gold, Is.Zero);
        Assert.That(Upgrades.NextPrice(line, miner), Is.EqualTo(Shop.SoldOut));
        miner.Gold = 99_999;
        Assert.That(Upgrades.TryBuy(line, ref miner), Is.False);
    }

    [Test]
    public void TryBuy_TooPoor_ChangesNothing()
    {
        var miner = new Miner { Gold = 299 };

        Assert.That(Upgrades.TryBuy(UpgradeLine.Shovel, ref miner), Is.False);
        Assert.That(miner.ShovelTier, Is.Zero);
        Assert.That(miner.Gold, Is.EqualTo(299));
    }

    [Test]
    public void ShopTryBuy_UpgradeItem_BuysTheLine()
    {
        var miner = new Miner { Gold = 400 };

        Assert.That(Shop.TryBuy(ShopItem.Detector, ref miner), Is.True);
        Assert.That(miner.DetectorTier, Is.EqualTo(1));
        Assert.That(Shop.Owned(ShopItem.Detector, miner), Is.EqualTo(1));
    }

    [Test]
    public void Effects_ByTier_MatchTheRules()
    {
        Assert.That(Upgrades.DigInterval(0), Is.EqualTo(MinerControlSystem.DigInterval));
        Assert.That(Upgrades.DigInterval(3), Is.EqualTo(MinerControlSystem.DigInterval * .55f).Within(.0001f));
        Assert.That(Upgrades.LampReach(3), Is.EqualTo(LightMap.LampRange + 6f));
        Assert.That(Upgrades.GlintRadius(2), Is.EqualTo(GlintSystem.Radius + 6));
        Assert.That(Upgrades.GlintInterval(1), Is.EqualTo(GlintSystem.Interval * .8f).Within(.0001f));
    }

    [Test]
    public void Control_BestShovel_DigsMoreHitsPerSecond()
    {
        Assert.That(HitsInOneSecond(shovelTier: 3), Is.GreaterThan(HitsInOneSecond(shovelTier: 0) + 3));
    }

    [Test]
    public void Compute_BetterLamp_LightsFurther()
    {
        var mine = new Mine(Seed);
        var row = Mine.SurfaceRow + 50;
        for (var column = 1; column < 20; column++)
            mine.At(column, row) = new TileCell { Type = TileType.Air };
        var light = new LightMap(Mine.Columns, 30);
        var origin = new Vector2(1.5f, row + .5f);

        light.Compute(mine, row - 10, 20, origin, Vector2.UnitX, Upgrades.LampReach(0), false);
        var startLamp = light.Get(11, row);
        light.Compute(mine, row - 10, 20, origin, Vector2.UnitX, Upgrades.LampReach(3), false);

        Assert.That(startLamp, Is.EqualTo(LightMap.Dark));
        Assert.That(light.Get(11, row), Is.Not.EqualTo(LightMap.Dark));
    }

    private static int HitsInOneSecond(int shovelTier)
    {
        var mine = new Mine(Seed);
        var row = Mine.SurfaceRow + 50;
        mine.At(5, row) = new TileCell { Type = TileType.Air };
        mine.At(5, row + 1) = new TileCell { Type = TileType.Stone };
        mine.At(6, row) = new TileCell { Type = TileType.Obsidian };
        var effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        var control = new MinerControlSystem(mine, new Elevator(), effects);
        var motion = new MinerMotionSystem(mine, effects);
        var commands = new CommandBuffer();
        var miner = new Miner
        {
            Column = 5, Row = row, MoveFrom = new Vector2(5, row), FacingX = 1, ShovelTier = shovelTier
        };
        var position = new Position(Layout.TileToWorld(5, row));
        var hits = 0;
        for (var frame = 0; frame < 60; frame++)
        {
            var before = miner.SwingTime;
            control.Control(ref miner, ref position, new MinerInput { Horizontal = 1 }, 1f / 60f, commands);
            if (miner.SwingTime > before)
                hits++;
            motion.Move(ref miner, ref position, 1f / 60f, commands);
            commands.Clear();
        }

        return hits;
    }
}
