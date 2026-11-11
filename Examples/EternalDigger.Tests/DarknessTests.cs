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

/// <summary>Darkness below the wall: light-swallowing act 2 rock and black damp areas.</summary>
public class DarknessTests
{
    private const int Seed = 31;

    [Test]
    public void DarkReach_BelowTheWall_LosesTwoTiles() =>
        Assert.That(LampOil.DarkReach(8f, belowTheWall: true, inBlackDamp: false), Is.EqualTo(6f));

    [Test]
    public void DarkReach_InBlackDamp_IsHalvedAfterTheAbsorb() =>
        Assert.That(LampOil.DarkReach(8f, belowTheWall: true, inBlackDamp: true), Is.EqualTo(3f));

    [Test]
    public void DarkReach_EmptyLamp_StaysDark() =>
        Assert.That(LampOil.DarkReach(0f, belowTheWall: true, inBlackDamp: true), Is.Zero);

    [Test]
    public void DarkReach_AboveTheWall_IsUnchanged() =>
        Assert.That(LampOil.DarkReach(8f, belowTheWall: false, inBlackDamp: false), Is.EqualTo(8f));

    [Test]
    public void IsBlackDamp_Generation_OnlyInOpenTilesBelowTheWallAndRare()
    {
        var mine = new Mine(Seed);
        var open = 0;
        var damp = 0;
        for (var row = Mine.SurfaceRow + 1; row < Mine.SurfaceRow + 260; row++)
        for (var column = 0; column < Mine.Columns; column++)
        {
            if (!mine.IsBlackDamp(column, row))
            {
                open += mine.IsBelowTheWall(column, row) && !mine.IsSolid(column, row) ? 1 : 0;
                continue;
            }

            damp++;
            Assert.That(mine.IsSolid(column, row), Is.False, $"tile {column}, {row}");
            Assert.That(mine.IsBelowTheWall(column, row), Is.True, $"tile {column}, {row}");
        }

        Assert.That(damp, Is.GreaterThan(0), "some black damp below the wall");
        Assert.That(damp, Is.LessThan(open), "most act 2 caves are clean air");
    }

    [Test]
    public void IsBlackDampEdge_RockBesideBlackDamp_IsAnEdge()
    {
        var mine = new Mine(Seed);
        var (column, row) = FindBlackDamp(mine);
        mine.At(column + 1, row) = new TileCell { Type = TileType.Slate };

        Assert.That(mine.IsBlackDampEdge(column + 1, row), Is.True);
        Assert.That(mine.IsBlackDampEdge(column, row), Is.False, "the open tile itself isn't an edge");
    }

    [Test]
    public void LampSystem_InBlackDamp_HalvesTheConeAndDoublesTheBurn()
    {
        var mine = new Mine(Seed);
        var (column, row) = FindBlackDamp(mine);
        var clean = Play(mine, column, row, inDamp: false, out var cleanOil);
        var damp = Play(mine, column, row, inDamp: true, out var dampOil);

        Assert.That(damp.Reach, Is.EqualTo(clean.Reach * LampOil.BlackDampReachShare).Within(.01f));
        Assert.That(LampOil.TankSize - dampOil, Is.EqualTo((LampOil.TankSize - cleanOil) * 2f).Within(.01f));
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static (int Column, int Row) FindBlackDamp(Mine mine)
    {
        for (var row = Mine.SurfaceRow + Tiles.WallDepth; row < Mine.SurfaceRow + 600; row++)
        for (var column = 1; column < Mine.Columns - 2; column++)
        {
            if (mine.IsBlackDamp(column, row))
                return (column, row);
        }

        throw new InvalidOperationException("no black damp in the first 600 m");
    }

    /// <summary>
    /// One second of <see cref="LampSystem"/> for a miner at a black damp tile, or (not <paramref name="inDamp"/>) at a
    /// clean open tile just like it below the wall. Returns the headlamp and the oil left.
    /// </summary>
    private static Headlamp Play(Mine mine, int column, int row, bool inDamp, out float oil)
    {
        var headlamp = new Headlamp();
        var useRow = row;
        var useColumn = column;
        if (!inDamp)
        {
            // A clean tile at the same depth: carve one far enough from the damp area.
            useColumn = column < Mine.Columns / 2 ? Mine.Columns - 2 : 1;
            mine.At(useColumn, useRow) = new TileCell { Type = TileType.Air };
            if (mine.IsBlackDamp(useColumn, useRow))
                Assert.Ignore("both sides are black damp with this seed");
        }

        using var world = new World();
        var group = new SystemGroup(world);
        group.Add(new LampSystem(headlamp, new EffectSpawner(new Random(Seed), new ScreenShake()), new Random(Seed),
            mine));
        var entity = world.Create(new Position(Layout.TileToWorld(useColumn, useRow)),
            new Miner { Column = useColumn, Row = useRow, Oil = LampOil.TankSize });
        group.Update(1f);
        oil = world.Get<Miner>(entity).Oil;
        return headlamp;
    }
}
