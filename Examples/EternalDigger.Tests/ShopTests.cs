using EternalDigger.Components;
using EternalDigger.Mining;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class ShopTests
{
    [Test]
    public void Deliver_FullBag_PaysEveryOreAndEmptiesTheBag()
    {
        var miner = new Miner { Gold = 5, CopperOre = 2, SilverOre = 1, GoldOre = 3, GemOre = 2 };

        var earned = Shop.Deliver(ref miner);

        var expected = 2 * Tiles.CopperValue + Tiles.SilverValue + 3 * Tiles.GoldValue + 2 * Tiles.GemValue;
        Assert.That(earned, Is.EqualTo(expected));
        Assert.That(miner.Gold, Is.EqualTo(5 + expected));
        Assert.That(miner.OreCount, Is.Zero);
    }

    [Test]
    public void Deliver_EmptyBag_ReturnsZero()
    {
        var miner = new Miner { Gold = 7 };

        Assert.That(Shop.Deliver(ref miner), Is.Zero);
        Assert.That(miner.Gold, Is.EqualTo(7));
    }

    [Test]
    public void TryBuyLadder_EnoughGold_TradesGoldForALadder()
    {
        var miner = new Miner { Gold = Shop.LadderPrice + 3, Ladders = 1 };

        Assert.That(Shop.TryBuyLadder(ref miner), Is.True);
        Assert.That(miner.Gold, Is.EqualTo(3));
        Assert.That(miner.Ladders, Is.EqualTo(2));
    }

    [Test]
    public void TryBuyLadder_NotEnoughGold_ChangesNothing()
    {
        var miner = new Miner { Gold = Shop.LadderPrice - 1, Ladders = 1 };

        Assert.That(Shop.TryBuyLadder(ref miner), Is.False);
        Assert.That(miner.Gold, Is.EqualTo(Shop.LadderPrice - 1));
        Assert.That(miner.Ladders, Is.EqualTo(1));
    }

    [Test]
    public void Collect_EachOre_GoesIntoItsSlot()
    {
        var miner = new Miner();

        Shop.Collect(ref miner, TileType.Copper);
        Shop.Collect(ref miner, TileType.Silver);
        Shop.Collect(ref miner, TileType.Silver);
        Shop.Collect(ref miner, TileType.Gold);
        Shop.Collect(ref miner, TileType.Gem);

        Assert.That(miner.GemOre, Is.EqualTo(1));
        Assert.That(miner.CopperOre, Is.EqualTo(1));
        Assert.That(miner.SilverOre, Is.EqualTo(2));
        Assert.That(miner.GoldOre, Is.EqualTo(1));
    }

    [Test]
    public void Collect_NotOre_ReturnsFalse()
    {
        var miner = new Miner();

        Assert.That(Shop.Collect(ref miner, TileType.Dirt), Is.False);
        Assert.That(miner.OreCount, Is.Zero);
    }

    [Test]
    public void TryBuyBlasting_FirstTime_BuysTheKitThenSticks()
    {
        var miner = new Miner { Gold = Shop.BlastingKitPrice + 2 * Shop.DynamitePrice };

        Assert.That(Shop.TryBuyBlasting(ref miner), Is.True);
        Assert.That(miner.HasBlastingKit, Is.True);
        Assert.That(miner.Dynamite, Is.Zero);
        Assert.That(Shop.BlastingPrice(miner), Is.EqualTo(Shop.DynamitePrice));

        Assert.That(Shop.TryBuyBlasting(ref miner), Is.True);
        Assert.That(Shop.TryBuyBlasting(ref miner), Is.True);
        Assert.That(miner.Dynamite, Is.EqualTo(2));
        Assert.That(miner.Gold, Is.Zero);
    }

    [Test]
    public void TryBuyBlasting_NotEnoughForTheKit_ChangesNothing()
    {
        var miner = new Miner { Gold = Shop.BlastingKitPrice - 1 };

        Assert.That(Shop.TryBuyBlasting(ref miner), Is.False);
        Assert.That(miner.HasBlastingKit, Is.False);
        Assert.That(miner.Gold, Is.EqualTo(Shop.BlastingKitPrice - 1));
    }

    [Test]
    public void CollectAndDeliver_RareFinds_SellHighAndCountAsFound()
    {
        var miner = new Miner();

        Shop.Collect(ref miner, TileType.Fossil);
        Shop.Collect(ref miner, TileType.Relic);
        var earned = Shop.Deliver(ref miner);

        Assert.That(earned, Is.EqualTo(Tiles.FossilValue + Tiles.RelicValue));
        Assert.That(miner.RareFinds, Is.Zero);
        Assert.That(miner.FossilsFound, Is.EqualTo(1));
        Assert.That(miner.RelicsFound, Is.EqualTo(1));
    }

    [Test]
    public void EmptyBag_WithFinds_KeepsTheFoundCounts()
    {
        var miner = new Miner();
        Shop.Collect(ref miner, TileType.Fossil);

        Shop.EmptyBag(ref miner);

        Assert.That(miner.Fossils, Is.Zero);
        Assert.That(miner.FossilsFound, Is.EqualTo(1));
    }
}
