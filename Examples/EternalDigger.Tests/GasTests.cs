using System;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>Gas pockets: where the mine puts them, burning a pocket, a lamp setting one off, and blasts.</summary>
public class GasTests
{
    private const int Seed = 21;
    private const int Column = 10;
    private const int Row = Mine.SurfaceRow + 70;

    private Mine _mine;
    private GasPockets _gas;
    private EffectSpawner _effects;
    private GasSystem _system;
    private CommandBuffer _commands;

    [SetUp]
    public void SetUp()
    {
        _mine = new Mine(Seed);
        _gas = new GasPockets(_mine);
        _effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        _system = new GasSystem(_mine, _gas, _effects, new Random(Seed));
        _commands = new CommandBuffer();
    }

    [Test]
    public void Generation_Gas_OnlyInCaveAirFromTheGasDepth()
    {
        var mine = new Mine(Seed);
        var found = 0;
        for (var row = Mine.SurfaceRow; row < Mine.SurfaceRow + 250; row++)
        for (var column = 0; column < Mine.Columns; column++)
        {
            var cell = mine.At(column, row);
            if (!cell.HasGas)
                continue;

            found++;
            Assert.That(cell.Type, Is.EqualTo(TileType.Air), $"tile {column}, {row}");
            Assert.That(Mine.Depth(row), Is.GreaterThanOrEqualTo(Tiles.GasDepth), $"tile {column}, {row}");
        }

        Assert.That(found, Is.GreaterThan(0), "the mine has some gas");
    }

    [Test]
    public void Ignite_Pocket_BurnsTheConnectedGasOnly()
    {
        ClearArea();
        SetGas(Column + 3, Row);
        SetGas(Column + 4, Row);
        SetGas(Column + 4, Row + 1);
        SetGas(Column + 8, Row);

        var burned = _gas.Ignite(Column + 3, Row);

        Assert.That(burned, Is.EqualTo(3));
        Assert.That(_mine.At(Column + 4, Row + 1).HasGas, Is.False);
        Assert.That(_mine.At(Column + 8, Row).HasGas, Is.True, "a separate pocket stays");
    }

    [Test]
    public void Ignite_HugePocket_BurnsAtMostTheLimitAndKeepsTheRest()
    {
        ClearArea();
        var total = 0;
        for (var r = Row - 3; r <= Row + 3; r++)
        for (var c = 2; c < Mine.Columns - 2; c++)
        {
            SetGas(c, r);
            total++;
        }

        var burned = _gas.Ignite(Column, Row);

        Assert.That(burned, Is.EqualTo(GasPockets.MaxPocket));
        Assert.That(CountGas(), Is.EqualTo(total - GasPockets.MaxPocket));
    }

    [Test]
    public void Check_LitLampNearGas_FlaresAndBlowsTheLampOut()
    {
        ClearArea();
        SetGas(Column + 2, Row);
        var miner = new Miner { Column = Column, Row = Row, Oil = 50f };

        var burned = _system.Check(ref miner, _commands);

        Assert.That(burned, Is.EqualTo(1));
        Assert.That(_mine.At(Column + 2, Row).HasGas, Is.False);
        Assert.That(miner.LampOut, Is.EqualTo(GasSystem.LampOutSeconds));
        Assert.That(miner.Oil, Is.EqualTo(50f - GasSystem.OilDrain));
        Assert.That(_effects.Shake.Strength, Is.GreaterThan(0f));
    }

    [Test]
    public void Check_GasOutOfReach_DoesNothing()
    {
        ClearArea();
        SetGas(Column + 3, Row);
        var miner = new Miner { Column = Column, Row = Row, Oil = 50f };

        Assert.That(_system.Check(ref miner, _commands), Is.Zero);
        Assert.That(miner.LampOut, Is.Zero);
    }

    [TestCase(0f, 0f)]
    [TestCase(50f, 1f)]
    public void Check_LampDryOrBlownOut_DoesNotSetGasOff(float oil, float lampOut)
    {
        ClearArea();
        SetGas(Column + 1, Row);
        var miner = new Miner { Column = Column, Row = Row, Oil = oil, LampOut = lampOut };

        Assert.That(_system.Check(ref miner, _commands), Is.Zero);
        Assert.That(_mine.At(Column + 1, Row).HasGas, Is.True);
    }

    [Test]
    public void LampSystem_BlownOut_IsDarkAndBurnsNoOil()
    {
        var headlamp = new Headlamp();
        using var world = new World();
        var group = new SystemGroup(world);
        group.Add(new LampSystem(headlamp, _effects, new Random(Seed)));
        var entity = world.Create(new ConsoleEngine.Ecs.Components.Position(Vector2.Zero),
            new Miner { Row = Row, Oil = 50f, LampOut = 1f });

        group.Update(.5f);

        Assert.That(headlamp.Reach, Is.Zero);
        Assert.That(headlamp.IsEmpty, Is.True);
        Assert.That(world.Get<Miner>(entity).Oil, Is.EqualTo(50f));
        Assert.That(world.Get<Miner>(entity).LampOut, Is.EqualTo(.5f).Within(.001f));
    }

    [Test]
    public void Blast_NearGas_BurnsItWithoutTouchingTheLamp()
    {
        ClearArea();
        SetGas(Column + 3, Row);
        var dynamite = new DynamiteSystem(_mine, _effects, _gas);
        var miner = new Miner { Column = Column - 3, Row = Row, Oil = 50f };

        dynamite.Blast(Column, Row, ref miner, _commands);

        Assert.That(_mine.At(Column + 3, Row).HasGas, Is.False);
        Assert.That(miner.LampOut, Is.Zero);
        Assert.That(miner.Oil, Is.EqualTo(50f));
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>A gas-free open hall of air around the test spot with a stone floor.</summary>
    private void ClearArea()
    {
        for (var r = Row - 6; r <= Row + 6; r++)
        for (var c = 1; c < Mine.Columns - 1; c++)
            _mine.At(c, r) = new TileCell { Type = r == Row + 6 ? TileType.Stone : TileType.Air };
    }

    private void SetGas(int column, int row) => _mine.At(column, row).HasGas = true;

    private int CountGas()
    {
        var count = 0;
        for (var r = Row - 6; r <= Row + 6; r++)
        for (var c = 0; c < Mine.Columns; c++)
            count += _mine.At(c, r).HasGas ? 1 : 0;

        return count;
    }
}
