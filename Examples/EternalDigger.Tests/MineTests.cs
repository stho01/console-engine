using System.Collections.Generic;
using EternalDigger.Mining;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class MineTests
{
    private const int Seed = 1234;
    private const int Column = 10;

    /// <summary>A row deep enough for every tile type, well below daylight.</summary>
    private const int DeepRow = Mine.SurfaceRow + 50;

    [Test]
    public void At_SameSeed_GeneratesSameTiles()
    {
        var first = new Mine(Seed);
        var second = new Mine(Seed);

        for (var row = 0; row < 200; row++)
        for (var column = 0; column < Mine.Columns; column++)
            Assert.That(second.TypeAt(column, row), Is.EqualTo(first.TypeAt(column, row)), $"({column}, {row})");
    }

    [Test]
    public void At_DifferentSeeds_GenerateDifferentTiles()
    {
        var first = new Mine(1);
        var second = new Mine(2);

        var differences = 0;
        for (var row = Mine.SurfaceRow + 1; row < 200; row++)
        for (var column = 1; column < Mine.Columns - 1; column++)
            if (first.TypeAt(column, row) != second.TypeAt(column, row))
                differences++;

        Assert.That(differences, Is.GreaterThan(100));
    }

    [Test]
    public void At_SkyRows_AreAir()
    {
        var mine = new Mine(Seed);

        for (var row = 0; row < Mine.SurfaceRow; row++)
        for (var column = 0; column < Mine.Columns; column++)
            Assert.That(mine.TypeAt(column, row), Is.EqualTo(TileType.Air));
    }

    [Test]
    public void At_SurfaceRow_IsGrassExceptTheShaft()
    {
        var mine = new Mine(Seed);

        for (var column = 0; column < Mine.Columns; column++)
        {
            var expected = column == Mine.ShaftColumn ? TileType.Air : TileType.Grass;
            Assert.That(mine.TypeAt(column, Mine.SurfaceRow), Is.EqualTo(expected), $"column {column}");
        }
    }

    [Test]
    public void At_Shaft_IsDugOutWithLaddersForTwoRows()
    {
        var mine = new Mine(Seed);

        for (var row = Mine.SurfaceRow; row <= Mine.SurfaceRow + 1; row++)
        {
            Assert.That(mine.TypeAt(Mine.ShaftColumn, row), Is.EqualTo(TileType.Air));
            Assert.That(mine.HasLadder(Mine.ShaftColumn, row), Is.True);
        }
    }

    [Test]
    public void At_SideColumnsBelowSurface_AreBedrock()
    {
        var mine = new Mine(Seed);

        for (var row = Mine.SurfaceRow + 1; row < 300; row++)
        {
            Assert.That(mine.TypeAt(0, row), Is.EqualTo(TileType.Bedrock));
            Assert.That(mine.TypeAt(Mine.Columns - 1, row), Is.EqualTo(TileType.Bedrock));
        }
    }

    [Test]
    public void At_RowBeyondGenerated_GeneratesWholeBlocks()
    {
        var mine = new Mine(Seed);
        Assert.That(mine.GeneratedRows, Is.EqualTo(Mine.RowsPerBlock));

        mine.At(Column, Mine.RowsPerBlock * 3 + 5);

        Assert.That(mine.GeneratedRows, Is.EqualTo(Mine.RowsPerBlock * 4));
    }

    [Test]
    public void At_OutsideColumns_IsBedrockAndIgnoresWrites()
    {
        var mine = new Mine(Seed);

        mine.At(-1, DeepRow) = new TileCell { Type = TileType.Air };

        Assert.That(mine.TypeAt(-1, DeepRow), Is.EqualTo(TileType.Bedrock));
        Assert.That(mine.TypeAt(Mine.Columns, Layout.GroundRow), Is.EqualTo(TileType.Bedrock));
    }

    [Test]
    public void At_AboveTheSky_IsAir() =>
        Assert.That(new Mine(Seed).TypeAt(Column, -1), Is.EqualTo(TileType.Air));

    [Test]
    public void Hit_Dirt_IsDamagedUntilTheLastHitBreaksIt()
    {
        var mine = MineWith(TileType.Dirt, Column, DeepRow);
        var hitsToBreak = Tiles.HitsToBreak(TileType.Dirt, Mine.Depth(DeepRow));

        for (var hit = 1; hit < hitsToBreak; hit++)
        {
            var result = mine.Hit(Column, DeepRow);
            Assert.That(result.Outcome, Is.EqualTo(DigOutcome.Damaged));
            Assert.That(result.Hits, Is.EqualTo(hit));
        }

        var last = mine.Hit(Column, DeepRow);

        Assert.That(last.Outcome, Is.EqualTo(DigOutcome.Broken));
        Assert.That(last.Type, Is.EqualTo(TileType.Dirt));
        Assert.That(mine.TypeAt(Column, DeepRow), Is.EqualTo(TileType.Air));
        Assert.That(mine.At(Column, DeepRow).Seen, Is.True);
    }

    [TestCase(TileType.Copper)]
    [TestCase(TileType.Silver)]
    [TestCase(TileType.Gold)]
    public void Hit_Ore_ReturnsTheOreWhenBroken(TileType ore)
    {
        var mine = MineWith(ore, Column, DeepRow);

        var result = Dig(mine, Column, DeepRow);

        Assert.That(result.Type, Is.EqualTo(ore));
    }

    [TestCase(TileType.Grass)]
    [TestCase(TileType.Bedrock)]
    [TestCase(TileType.Air)]
    public void Hit_Unbreakable_DoesNothing(TileType type)
    {
        var mine = MineWith(type, Column, DeepRow);

        var result = mine.Hit(Column, DeepRow);

        Assert.That(result.Outcome, Is.EqualTo(DigOutcome.Nothing));
        Assert.That(mine.TypeAt(Column, DeepRow), Is.EqualTo(type));
    }

    [Test]
    public void Hit_OutsideTheMine_DoesNothing() =>
        Assert.That(new Mine(Seed).Hit(-1, DeepRow).Outcome, Is.EqualTo(DigOutcome.Nothing));

    [Test]
    public void IsSupported_LadderInOwnTile_ReturnsTrue()
    {
        var mine = MineWith(TileType.Air, Column, DeepRow);
        mine.At(Column, DeepRow + 1) = new TileCell { Type = TileType.Air };
        mine.TryPlaceLadder(Column, DeepRow);

        Assert.That(mine.IsSupported(Column, DeepRow), Is.True);
    }

    [Test]
    public void IsSupported_SolidBelow_ReturnsTrue()
    {
        var mine = MineWith(TileType.Air, Column, DeepRow);
        mine.At(Column, DeepRow + 1) = new TileCell { Type = TileType.Stone };

        Assert.That(mine.IsSupported(Column, DeepRow), Is.True);
    }

    [Test]
    public void IsSupported_LadderBelow_ReturnsTrue()
    {
        var mine = MineWith(TileType.Air, Column, DeepRow);
        mine.At(Column, DeepRow + 1) = new TileCell { Type = TileType.Air, HasLadder = true };

        Assert.That(mine.IsSupported(Column, DeepRow), Is.True);
    }

    [Test]
    public void IsSupported_AirBelowWithoutLadders_ReturnsFalse()
    {
        var mine = MineWith(TileType.Air, Column, DeepRow);
        mine.At(Column, DeepRow + 1) = new TileCell { Type = TileType.Air };

        Assert.That(mine.IsSupported(Column, DeepRow), Is.False);
    }

    [Test]
    public void TryPlaceLadder_DugOutTileBelowSurface_PlacesOnce()
    {
        var mine = MineWith(TileType.Air, Column, DeepRow);

        Assert.That(mine.TryPlaceLadder(Column, DeepRow), Is.True);
        Assert.That(mine.HasLadder(Column, DeepRow), Is.True);
        Assert.That(mine.TryPlaceLadder(Column, DeepRow), Is.False);
    }

    [Test]
    public void TryTakeLadder_PlacedLadder_TakesItOnce()
    {
        var mine = MineWith(TileType.Air, Column, DeepRow);
        mine.TryPlaceLadder(Column, DeepRow);

        Assert.That(mine.TryTakeLadder(Column, DeepRow), Is.True);
        Assert.That(mine.HasLadder(Column, DeepRow), Is.False);
        Assert.That(mine.TryTakeLadder(Column, DeepRow), Is.False);
    }

    [Test]
    public void TryTakeLadder_GrassRowShaft_KeepsTheEntranceLadder() =>
        Assert.That(new Mine(Seed).TryTakeLadder(Mine.ShaftColumn, Mine.SurfaceRow), Is.False);

    [Test]
    public void TryPlaceLadder_SolidTile_ReturnsFalse() =>
        Assert.That(MineWith(TileType.Stone, Column, DeepRow).TryPlaceLadder(Column, DeepRow), Is.False);

    [TestCase(Layout.GroundRow)]
    [TestCase(Mine.SurfaceRow)]
    public void TryPlaceLadder_SkyOrSurface_ReturnsFalse(int row) =>
        Assert.That(MineWith(TileType.Air, Column, row).TryPlaceLadder(Column, row), Is.False);

    [Test]
    public void TryPlaceLadder_OutsideTheMine_ReturnsFalse() =>
        Assert.That(new Mine(Seed).TryPlaceLadder(-1, DeepRow), Is.False);

    [TestCase(TileType.Copper, Tiles.CopperDepth)]
    [TestCase(TileType.Silver, Tiles.SilverDepth)]
    [TestCase(TileType.Gold, Tiles.GoldDepth)]
    [TestCase(TileType.Granite, Tiles.GraniteDepth)]
    [TestCase(TileType.Obsidian, Tiles.WallDepth)]
    [TestCase(TileType.Slate, Tiles.WallDepth + Tiles.WallMinThickness)]
    [TestCase(TileType.Quartzite, Tiles.WallDepth + Tiles.WallMinThickness)]
    [TestCase(TileType.Gem, Tiles.WallDepth + Tiles.WallMinThickness)]
    public void At_Generation_NeverPlacesATypeAboveItsDepth(TileType type, int firstDepth)
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var mine = new Mine(seed);
            for (var row = 0; row < Mine.SurfaceRow + firstDepth; row++)
            for (var column = 0; column < Mine.Columns; column++)
                Assert.That(mine.TypeAt(column, row), Is.Not.EqualTo(type), $"seed {seed} ({column}, {row})");
        }
    }

    [TestCase(TileType.Copper)]
    [TestCase(TileType.Silver)]
    [TestCase(TileType.Gold)]
    [TestCase(TileType.Granite)]
    [TestCase(TileType.Obsidian)]
    [TestCase(TileType.Slate)]
    [TestCase(TileType.Quartzite)]
    [TestCase(TileType.Gem)]
    public void At_Generation_EventuallyPlacesEveryType(TileType type)
    {
        var mine = new Mine(Seed);

        var found = 0;
        for (var row = Mine.SurfaceRow; row < Mine.SurfaceRow + 150; row++)
        for (var column = 0; column < Mine.Columns; column++)
            if (mine.TypeAt(column, row) == type)
                found++;

        Assert.That(found, Is.GreaterThan(5));
    }

    [Test]
    public void At_Wall_IsObsidianThreeToFiveThickInEveryInnerColumn()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var mine = new Mine(seed);
            for (var column = 1; column < Mine.Columns - 1; column++)
            {
                var bottom = mine.WallBottomDepth(column);
                var thickness = bottom - Tiles.WallDepth + 1;
                Assert.That(thickness, Is.InRange(Tiles.WallMinThickness, Tiles.WallMaxThickness));
                for (var depth = Tiles.WallDepth; depth <= bottom; depth++)
                    Assert.That(mine.TypeAt(column, Mine.SurfaceRow + depth), Is.EqualTo(TileType.Obsidian),
                        $"seed {seed} column {column} depth {depth}");
                Assert.That(mine.TypeAt(column, Mine.SurfaceRow + bottom + 1), Is.Not.EqualTo(TileType.Obsidian));
            }

            Assert.That(mine.TypeAt(0, Mine.SurfaceRow + Tiles.WallDepth), Is.EqualTo(TileType.Bedrock));
        }
    }

    [Test]
    public void At_BelowTheWall_HasNoAct1RockOrOre()
    {
        var mine = new Mine(Seed);
        for (var column = 1; column < Mine.Columns - 1; column++)
        for (var depth = mine.WallBottomDepth(column) + 1; depth < Tiles.WallDepth + 120; depth++)
        {
            var type = mine.TypeAt(column, Mine.SurfaceRow + depth);
            Assert.That(type, Is.AnyOf(TileType.Slate, TileType.Quartzite, TileType.Gem, TileType.Relic, TileType.Air),
                $"column {column} depth {depth}");
        }
    }

    [Test]
    public void At_RareFinds_FossilsInAct1AndRelicsBelowTheWallOnly()
    {
        int fossils = 0, relics = 0, act1Tiles = 0;
        for (var seed = 0; seed < 10; seed++)
        {
            var mine = new Mine(seed);
            for (var column = 1; column < Mine.Columns - 1; column++)
            for (var depth = 0; depth < Tiles.WallDepth + 200; depth++)
            {
                var type = mine.TypeAt(column, Mine.SurfaceRow + depth);
                var act2 = depth > mine.WallBottomDepth(column);
                if (!act2 && depth >= Tiles.FossilDepth && depth < Tiles.WallDepth)
                    act1Tiles++;
                if (type == TileType.Fossil)
                {
                    fossils++;
                    Assert.That(depth, Is.InRange(Tiles.FossilDepth, Tiles.WallDepth - 1), $"seed {seed}");
                }

                if (type == TileType.Relic)
                {
                    relics++;
                    Assert.That(act2, Is.True, $"seed {seed} depth {depth}");
                }
            }
        }

        Assert.That(fossils, Is.GreaterThan(0));
        Assert.That(relics, Is.GreaterThan(0));
        Assert.That(fossils, Is.LessThan(act1Tiles * Tiles.FossilChance * 3), "rare");
    }

    [Test]
    public void Hit_Obsidian_DoesNothing() =>
        Assert.That(MineWith(TileType.Obsidian, Column, DeepRow).Hit(Column, DeepRow).Outcome,
            Is.EqualTo(DigOutcome.Nothing));

    //**********************************************************
    //** helpers
    //**********************************************************

    private static Mine MineWith(TileType type, int column, int row)
    {
        var mine = new Mine(Seed);
        mine.At(column, row) = new TileCell { Type = type };
        return mine;
    }

    private static DigResult Dig(Mine mine, int column, int row)
    {
        DigResult result;
        do
            result = mine.Hit(column, row);
        while (result.Outcome == DigOutcome.Damaged);

        return result;
    }
    [Test]
    public void CollectChanges_FreshMine_IsEmpty()
    {
        var mine = new Mine(Seed);
        mine.At(0, Mine.SurfaceRow + 300);
        var changes = new List<TileChange>();

        mine.CollectChanges(changes);

        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void CollectChanges_AfterDiggingAndALadder_ListsOnlyThoseTiles()
    {
        var mine = new Mine(Seed);
        var row = Mine.SurfaceRow + 5;
        mine.At(4, row) = new TileCell { Type = TileType.Air };
        mine.TryPlaceLadder(4, row);
        mine.At(5, row).Damage = 1;
        var changes = new List<TileChange>();

        mine.CollectChanges(changes);

        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes[0].Cell.HasLadder, Is.True);
        Assert.That(changes[1].Cell.Damage, Is.EqualTo(1));
    }

    [Test]
    public void Apply_ChangeOutsideTheMine_IsIgnored()
    {
        var mine = new Mine(Seed);

        mine.Apply(new TileChange(Mine.Columns, 20, new TileCell { Type = TileType.Air }));

        Assert.That(mine.TypeAt(Mine.Columns, 20), Is.EqualTo(TileType.Bedrock));
    }
}
