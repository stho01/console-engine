using EternalDigger.Mining;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class TilesTests
{
    [TestCase(TileType.Air, false)]
    [TestCase(TileType.Grass, true)]
    [TestCase(TileType.Dirt, true)]
    [TestCase(TileType.Bedrock, true)]
    [TestCase(TileType.Gold, true)]
    public void IsSolid_Type_ReturnsExpected(TileType type, bool expected) =>
        Assert.That(Tiles.IsSolid(type), Is.EqualTo(expected));

    [TestCase(TileType.Air, false)]
    [TestCase(TileType.Grass, false)]
    [TestCase(TileType.Bedrock, false)]
    [TestCase(TileType.Dirt, true)]
    [TestCase(TileType.Stone, true)]
    [TestCase(TileType.Granite, true)]
    [TestCase(TileType.Copper, true)]
    public void IsBreakable_Type_ReturnsExpected(TileType type, bool expected) =>
        Assert.That(Tiles.IsBreakable(type), Is.EqualTo(expected));

    [TestCase(TileType.Copper, Tiles.CopperValue)]
    [TestCase(TileType.Silver, Tiles.SilverValue)]
    [TestCase(TileType.Gold, Tiles.GoldValue)]
    [TestCase(TileType.Stone, 0)]
    public void OreValue_Type_ReturnsExpected(TileType type, int expected)
    {
        Assert.That(Tiles.OreValue(type), Is.EqualTo(expected));
        Assert.That(Tiles.IsOre(type), Is.EqualTo(expected > 0));
    }

    [Test]
    public void OreValue_DeeperOres_AreWorthMore()
    {
        Assert.That(Tiles.SilverValue, Is.GreaterThan(Tiles.CopperValue));
        Assert.That(Tiles.GoldValue, Is.GreaterThan(Tiles.SilverValue));
    }

    [TestCase(0, 2)]
    [TestCase(Tiles.DepthPerExtraHit - 1, 2)]
    [TestCase(Tiles.DepthPerExtraHit, 3)]
    [TestCase(Tiles.DepthPerExtraHit * 4, 6)]
    public void HitsToBreak_DirtAtDepth_GainsAHitPerStep(int depth, int expected) =>
        Assert.That(Tiles.HitsToBreak(TileType.Dirt, depth), Is.EqualTo(expected));

    [TestCase(TileType.Air)]
    [TestCase(TileType.Grass)]
    [TestCase(TileType.Bedrock)]
    public void HitsToBreak_Unbreakable_ReturnsZero(TileType type) =>
        Assert.That(Tiles.HitsToBreak(type, 100), Is.Zero);
}
