using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// Planting and blasting frame by frame: the miner's rules and motion with scripted input, and the real
/// <see cref="DynamiteSystem"/> in a <see cref="SystemGroup"/>, so sticks are real entities.
/// </summary>
public class DynamiteTests
{
    private const float FrameSeconds = 1f / 60f;
    private const int Seed = 11;
    private const int Column = 10;
    private const int Row = Mine.SurfaceRow + 50;

    private World _world;
    private Mine _mine;
    private EffectSpawner _effects;
    private SystemGroup _simulation;
    private MinerControlSystem _control;
    private MinerMotionSystem _motion;
    private DynamiteSystem _dynamite;
    private Entity _miner;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _mine = new Mine(Seed);
        _effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        var elevator = new Elevator();
        _control = new MinerControlSystem(_mine, elevator, _effects);
        _motion = new MinerMotionSystem(_mine, _effects);
        _simulation = new SystemGroup(_world);
        _dynamite = _simulation.Add(new DynamiteSystem(_mine, _effects));
        _simulation.Add(new ParticleLifetimeSystem());

        // A pocket: the miner's tile and one to the left are open, stone under both, stone to the right.
        _mine.At(Column - 1, Row) = new TileCell { Type = TileType.Air };
        _mine.At(Column, Row) = new TileCell { Type = TileType.Air };
        _mine.At(Column - 1, Row + 1) = new TileCell { Type = TileType.Stone };
        _mine.At(Column, Row + 1) = new TileCell { Type = TileType.Stone };
        _mine.At(Column + 1, Row) = new TileCell { Type = TileType.Stone };
        _miner = _world.Create(
            new Position(Layout.TileToWorld(Column, Row)),
            new Miner
            {
                Column = Column, Row = Row, MoveFrom = new Vector2(Column, Row), FacingX = 1,
                LampDirection = Vector2.UnitX, HasBlastingKit = true, Dynamite = 3
            });
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Plant_HoldUse_PlantsALitStickInTheFacedTile()
    {
        Play(new MinerInput { Use = true }, MinerControlSystem.PlantDuration + .05f);

        Assert.That(Sticks(), Is.EqualTo(1));
        Assert.That(Miner.Dynamite, Is.EqualTo(2));
        Assert.That(Miner.LitDynamite, Is.EqualTo(1));
        Assert.That(FirstStick().Column, Is.EqualTo(Column + 1));
        Assert.That(FirstStick().Row, Is.EqualTo(Row));
    }

    [Test]
    public void Plant_HoldUseWithDown_PlantsBelowTheMiner()
    {
        Play(new MinerInput { Use = true, Down = true }, MinerControlSystem.PlantDuration + .05f);

        Assert.That(Sticks(), Is.EqualTo(1));
        Assert.That(FirstStick().Column, Is.EqualTo(Column));
        Assert.That(FirstStick().Row, Is.EqualTo(Row + 1));
        Assert.That(Miner.Row, Is.EqualTo(Row), "holding E keeps the down key from digging");
    }

    [Test]
    public void Plant_ReleaseEarly_PlantsNothing()
    {
        Play(new MinerInput { Use = true }, MinerControlSystem.PlantDuration / 2f);
        Play(default, FrameSeconds);

        Assert.That(Sticks(), Is.Zero);
        Assert.That(Miner.Dynamite, Is.EqualTo(3));
        Assert.That(Miner.PlantHold, Is.Zero);
    }

    [Test]
    public void Plant_KeepHolding_PlantsOnlyOneStick()
    {
        Play(new MinerInput { Use = true }, MinerControlSystem.FuseDuration * .8f);

        Assert.That(Sticks(), Is.EqualTo(1));
        Assert.That(Miner.Dynamite, Is.EqualTo(2));
    }

    [Test]
    public void Plant_WithoutTheKit_PlantsNothing()
    {
        _world.Get<Miner>(_miner).HasBlastingKit = false;

        Play(new MinerInput { Use = true }, MinerControlSystem.PlantDuration + .05f);

        Assert.That(Sticks(), Is.Zero);
        Assert.That(Miner.Dynamite, Is.EqualTo(3));
    }

    [Test]
    public void Plant_FacingAir_PlantsNothing()
    {
        _world.Get<Miner>(_miner).LampDirection = -Vector2.UnitX;

        Play(new MinerInput { Use = true }, MinerControlSystem.PlantDuration + .05f);

        Assert.That(Sticks(), Is.Zero);
    }

    [Test]
    public void Blast_AfterTheFuse_ClearsTheAreaBagsOreAndPushesTheMiner()
    {
        _mine.At(Column + 1, Row - 1) = new TileCell { Type = TileType.Obsidian };
        _mine.At(Column + 2, Row) = new TileCell { Type = TileType.Gold };
        _mine.At(Column, Row) = new TileCell { Type = TileType.Air, HasLadder = true };

        Play(new MinerInput { Use = true }, MinerControlSystem.PlantDuration + .05f);
        Play(default, MinerControlSystem.FuseDuration + .3f);

        for (var r = Row - 1; r <= Row + 1; r++)
        for (var c = Column; c <= Column + 2; c++)
            Assert.That(_mine.TypeAt(c, r), Is.EqualTo(TileType.Air), $"tile {c}, {r}");
        Assert.That(_mine.HasLadder(Column, Row), Is.False, "the ladder in the blast is gone");
        Assert.That(Miner.GoldOre, Is.EqualTo(1));
        Assert.That(Miner.Column, Is.EqualTo(Column - 1), "pushed one tile away");
        Assert.That(Sticks(), Is.Zero);
        Assert.That(Miner.LitDynamite, Is.Zero);
    }

    [Test]
    public void Blast_NextToTheSideWall_LeavesTheBedrock()
    {
        var miner = new Miner();
        _dynamite.Blast(1, Row, ref miner, _simulation.Commands);

        Assert.That(_mine.TypeAt(0, Row), Is.EqualTo(TileType.Bedrock));
        Assert.That(_mine.TypeAt(2, Row), Is.EqualTo(TileType.Air));
    }

    [Test]
    public void Blast_UnderTheGrass_LeavesTheSurface()
    {
        var miner = new Miner();
        _dynamite.Blast(5, Mine.SurfaceRow + 1, ref miner, _simulation.Commands);

        Assert.That(_mine.TypeAt(5, Mine.SurfaceRow), Is.EqualTo(TileType.Grass));
        Assert.That(_mine.TypeAt(5, Mine.SurfaceRow + 2), Is.EqualTo(TileType.Air));
    }

    [Test]
    public void Blast_ReachingAnotherStick_SetsItOffRightAfter()
    {
        _effects.PlantDynamite(_simulation.Commands, Column + 3, Row, fuse: .5f);
        _effects.PlantDynamite(_simulation.Commands, Column + 4, Row, fuse: 10f);
        _simulation.Update(0f);

        Play(default, .5f + DynamiteSystem.ChainDelay + .1f);

        Assert.That(Sticks(), Is.Zero);
    }

    [Test]
    public void Plant_OnTheSurface_PlantsNothing()
    {
        ref var miner = ref _world.Get<Miner>(_miner);
        miner.Column = Layout.SpawnColumn;
        miner.Row = Layout.GroundRow;
        miner.LampDirection = Vector2.UnitY;

        Play(new MinerInput { Use = true }, MinerControlSystem.PlantDuration + .05f);

        Assert.That(Sticks(), Is.Zero);
        Assert.That(Miner.PlantHold, Is.Zero);
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private Miner Miner => _world.Get<Miner>(_miner);

    private void Play(MinerInput input, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += FrameSeconds)
        {
            ref var miner = ref _world.Get<Miner>(_miner);
            ref var position = ref _world.Get<Position>(_miner);
            _control.Control(ref miner, ref position, input, FrameSeconds, _simulation.Commands);
            _motion.Move(ref miner, ref position, FrameSeconds, _simulation.Commands);
            _simulation.Update(FrameSeconds);
        }
    }

    private int Sticks()
    {
        var count = 0;
        foreach (var chunk in _world.Query(Query.With<Dynamite>()).GetChunks())
            count += chunk.Count;

        return count;
    }

    private Dynamite FirstStick()
    {
        foreach (var chunk in _world.Query(Query.With<Dynamite>()).GetChunks())
            return chunk.GetSpan<Dynamite>()[0];

        throw new InvalidOperationException("no stick");
    }
}
