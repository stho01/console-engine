using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Systems;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// Plays frames of <see cref="MinerControlSystem.Control"/> followed by <see cref="MinerMotionSystem.Move"/>, like
/// the scene does, with scripted input instead of the keyboard.
/// </summary>
public class MinerControlSystemTests
{
    private const float FrameSeconds = 1f / 60f;
    private const int Seed = 7;

    /// <summary>The first row of the shaft that isn't dug out at the start.</summary>
    private const int ShaftBottom = Mine.SurfaceRow + 2;

    private Mine _mine;
    private MinerControlSystem _control;
    private MinerMotionSystem _motion;
    private CommandBuffer _commands;
    private Miner _miner;
    private Position _position;

    [SetUp]
    public void SetUp()
    {
        _mine = new Mine(Seed);
        var effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        _control = new MinerControlSystem(_mine, new Elevator(), effects);
        _motion = new MinerMotionSystem(_mine, effects);
        _commands = new CommandBuffer();
        PlaceMiner(Layout.SpawnColumn, Layout.GroundRow);
        _miner.Ladders = Shop.StartLadders;
    }

    [Test]
    public void Control_HoldRight_WalksAlongTheSurface()
    {
        Play(new MinerInput { Horizontal = 1 }, seconds: 1f);

        Assert.That(_miner.Column, Is.GreaterThan(Layout.SpawnColumn + 3));
        Assert.That(_miner.Row, Is.EqualTo(Layout.GroundRow));
        Assert.That(_miner.FacingX, Is.EqualTo(1));
    }

    [Test]
    public void Control_HoldLeftAtTheEdge_StopsAtTheFirstColumn()
    {
        Play(new MinerInput { Horizontal = -1 }, seconds: 5f);

        Assert.That(_miner.Column, Is.Zero);
        Assert.That(_miner.Row, Is.EqualTo(Layout.GroundRow));
    }

    [Test]
    public void Control_HoldDownOnGrass_CannotDig()
    {
        Play(new MinerInput { Down = true }, seconds: 2f);

        Assert.That(_miner.Row, Is.EqualTo(Layout.GroundRow));
        Assert.That(_mine.TypeAt(Layout.SpawnColumn, Mine.SurfaceRow), Is.EqualTo(TileType.Grass));
    }

    [Test]
    public void Control_HoldDownInTheShaft_ClimbsDownAndDigsDeeper()
    {
        FillShaft(TileType.Dirt, ShaftBottom, ShaftBottom + 6);
        PlaceMiner(Mine.ShaftColumn, Layout.GroundRow);

        Play(new MinerInput { Down = true }, seconds: 4f);

        Assert.That(_miner.Row, Is.GreaterThan(ShaftBottom + 2));
        Assert.That(_mine.TypeAt(Mine.ShaftColumn, ShaftBottom), Is.EqualTo(TileType.Air));
        Assert.That(_position.Value, Is.EqualTo(Layout.TileToWorld(_miner.Column, _miner.Row)));
    }

    [Test]
    public void Control_UpWithoutALadder_StaysDown()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom + 2);
        FillShaft(TileType.Stone, ShaftBottom + 3, ShaftBottom + 3);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom + 2);

        Play(new MinerInput { Up = true }, seconds: 2f);

        Assert.That(_miner.Row, Is.EqualTo(ShaftBottom + 2));
    }

    [Test]
    public void Control_LaddersAllTheWay_ClimbsBackToTheSurface()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom + 3);
        FillShaft(TileType.Stone, ShaftBottom + 4, ShaftBottom + 4);
        for (var row = ShaftBottom; row < ShaftBottom + 3; row++)
            _mine.TryPlaceLadder(Mine.ShaftColumn, row);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom + 3);

        Play(new MinerInput { PlaceLadder = true }, seconds: FrameSeconds);
        Play(new MinerInput { Up = true }, seconds: 3f);

        Assert.That(_miner.Ladders, Is.EqualTo(Shop.StartLadders - 1));
        Assert.That(_miner.Row, Is.EqualTo(Layout.GroundRow));
        Assert.That(_miner.Column, Is.EqualTo(Mine.ShaftColumn));
    }

    [Test]
    public void Control_LadderKeyOnALadder_TakesItBackAndPlacesItAgain()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom);
        FillShaft(TileType.Stone, ShaftBottom + 1, ShaftBottom + 1);
        _mine.TryPlaceLadder(Mine.ShaftColumn, ShaftBottom);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom);

        Play(new MinerInput { PlaceLadder = true }, seconds: FrameSeconds);

        Assert.That(_mine.HasLadder(Mine.ShaftColumn, ShaftBottom), Is.False);
        Assert.That(_miner.Ladders, Is.EqualTo(Shop.StartLadders + 1));

        Play(new MinerInput { PlaceLadder = true }, seconds: FrameSeconds);

        Assert.That(_mine.HasLadder(Mine.ShaftColumn, ShaftBottom), Is.True);
        Assert.That(_miner.Ladders, Is.EqualTo(Shop.StartLadders));
    }

    [Test]
    public void Control_TakeTheLadderHoldingTheMiner_Falls()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom + 2);
        FillShaft(TileType.Stone, ShaftBottom + 3, ShaftBottom + 3);
        _mine.TryPlaceLadder(Mine.ShaftColumn, ShaftBottom);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom);

        Play(new MinerInput { PlaceLadder = true }, seconds: FrameSeconds);
        Play(default, .5f);

        Assert.That(_miner.Row, Is.EqualTo(ShaftBottom + 2));
    }

    [Test]
    public void Control_PlaceLadderWithoutLadders_PlacesNothing()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom);
        FillShaft(TileType.Stone, ShaftBottom + 1, ShaftBottom + 1);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom);
        _miner.Ladders = 0;

        Play(new MinerInput { PlaceLadder = true }, seconds: FrameSeconds);

        Assert.That(_mine.HasLadder(Mine.ShaftColumn, ShaftBottom), Is.False);
    }

    [Test]
    public void Control_NothingBelow_FallsAndLands()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom + 5);
        FillShaft(TileType.Stone, ShaftBottom + 6, ShaftBottom + 6);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom);

        Play(default, seconds: 2f);

        Assert.That(_miner.Row, Is.EqualTo(ShaftBottom + 5));
        Assert.That(_miner.Motion, Is.EqualTo(MinerMotion.Idle));
        Assert.That(_miner.FallRows, Is.Zero, "reset when landing");
    }

    [Test]
    public void Control_DigOre_PutsItInTheBag()
    {
        FillShaft(TileType.Gold, ShaftBottom, ShaftBottom);
        FillShaft(TileType.Stone, ShaftBottom + 1, ShaftBottom + 1);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom - 1);

        Play(new MinerInput { Down = true }, seconds: 3f);

        Assert.That(_miner.GoldOre, Is.EqualTo(1));
    }

    [Test]
    public void Control_HoldRescue_ReturnsToTheSpawnWithAnEmptyBag()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom);
        FillShaft(TileType.Stone, ShaftBottom + 1, ShaftBottom + 1);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom);
        _miner.CopperOre = 3;
        _miner.Gold = 50;

        Play(new MinerInput { Rescue = true }, seconds: MinerControlSystem.RescueDuration + .1f);

        Assert.That(_miner.Column, Is.EqualTo(Layout.SpawnColumn));
        Assert.That(_miner.Row, Is.EqualTo(Layout.GroundRow));
        Assert.That(_miner.OreCount, Is.Zero);
        Assert.That(_miner.Gold, Is.EqualTo(50));
        Assert.That(_position.Value, Is.EqualTo(Layout.TileToWorld(Layout.SpawnColumn, Layout.GroundRow)));
    }

    [Test]
    public void Control_ReleaseRescueEarly_StaysAndResetsTheTimer()
    {
        FillShaft(TileType.Air, ShaftBottom, ShaftBottom);
        FillShaft(TileType.Stone, ShaftBottom + 1, ShaftBottom + 1);
        PlaceMiner(Mine.ShaftColumn, ShaftBottom);

        Play(new MinerInput { Rescue = true }, seconds: MinerControlSystem.RescueDuration / 2f);
        Play(default, seconds: FrameSeconds);

        Assert.That(_miner.Row, Is.EqualTo(ShaftBottom));
        Assert.That(_miner.RescueHold, Is.Zero);
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    [Test]
    public void Control_HoldUpOnA100mLadder_ClimbsOutInSevenToEightSeconds()
    {
        const int depth = 100;
        LadderShaft(Mine.SurfaceRow + depth);
        PlaceMiner(Mine.ShaftColumn, Mine.SurfaceRow + depth);

        var seconds = SecondsUntil(new MinerInput { Up = true }, () => _miner.Row == Layout.GroundRow, limit: 20f);

        Assert.That(seconds, Is.InRange(7f, 8f));
    }

    [Test]
    public void Control_ShortClimb_KeepsTheStartSpeed()
    {
        LadderShaft(Mine.SurfaceRow + 20);
        PlaceMiner(Mine.ShaftColumn, Mine.SurfaceRow + 20);
        var target = _miner.Row - MinerControlSystem.ClimbRampStart;

        var seconds = SecondsUntil(new MinerInput { Up = true }, () => _miner.Row == target && !_miner.IsMoving, 5f);

        // One frame is lost at the end of each tile.
        var expected = MinerControlSystem.ClimbRampStart * MinerControlSystem.ClimbDuration;
        Assert.That(seconds, Is.InRange(expected, expected + MinerControlSystem.ClimbRampStart * FrameSeconds + .001f));
    }

    [Test]
    public void Control_ReleaseUpDuringALongClimb_StartsSlowAgain()
    {
        LadderShaft(Mine.SurfaceRow + 60);
        PlaceMiner(Mine.ShaftColumn, Mine.SurfaceRow + 60);
        Play(new MinerInput { Up = true }, seconds: 3f);
        Assert.That(_miner.MoveDuration, Is.EqualTo(MinerControlSystem.TopClimbDuration).Within(.001f));

        SecondsUntil(default, () => !_miner.IsMoving, 1f);
        Play(default, seconds: FrameSeconds);
        Play(new MinerInput { Up = true }, seconds: FrameSeconds);

        Assert.That(_miner.MoveDuration, Is.EqualTo(MinerControlSystem.ClimbDuration));
    }

    [Test]
    public void Control_DigAFossil_BagsItAndCountsIt()
    {
        var row = Mine.SurfaceRow + 30;
        _mine.At(5, row) = new TileCell { Type = TileType.Air };
        _mine.At(5, row + 1) = new TileCell { Type = TileType.Stone };
        _mine.At(6, row) = new TileCell { Type = TileType.Fossil };
        PlaceMiner(5, row);

        Play(new MinerInput { Horizontal = 1 }, seconds: 2f);

        Assert.That(_mine.TypeAt(6, row), Is.EqualTo(TileType.Air));
        Assert.That(_miner.Fossils, Is.EqualTo(1));
        Assert.That(_miner.FossilsFound, Is.EqualTo(1));
    }

    [Test]
    public void Control_DigObsidian_ClanksWithoutDamage()
    {
        var row = Mine.SurfaceRow + Tiles.WallDepth - 1;
        _mine.At(5, row) = new TileCell { Type = TileType.Air };
        _mine.At(5, row + 1) = new TileCell { Type = TileType.Obsidian };
        PlaceMiner(5, row);

        Play(new MinerInput { Down = true }, seconds: 3f);

        Assert.That(_mine.At(5, row + 1).Type, Is.EqualTo(TileType.Obsidian));
        Assert.That(_mine.At(5, row + 1).Damage, Is.Zero);
        Assert.That(_miner.Row, Is.EqualTo(row));
        Assert.That(_miner.SwingDirection.Y, Is.EqualTo(1), "the shovel still swings");
    }

    [Test]
    public void ClimbUpDuration_ByStreak_RampsFromStartToTopSpeed()
    {
        Assert.That(MinerControlSystem.ClimbUpDuration(0), Is.EqualTo(MinerControlSystem.ClimbDuration));
        Assert.That(MinerControlSystem.ClimbUpDuration(MinerControlSystem.ClimbRampStart - 1),
            Is.EqualTo(MinerControlSystem.ClimbDuration));
        Assert.That(MinerControlSystem.ClimbUpDuration(MinerControlSystem.ClimbRampStart),
            Is.LessThan(MinerControlSystem.ClimbDuration));
        Assert.That(MinerControlSystem.ClimbUpDuration(MinerControlSystem.ClimbRampStart + 100),
            Is.EqualTo(MinerControlSystem.TopClimbDuration).Within(.0001f));
    }

    [Test]
    public void Control_UpAndRightAtAGap_JumpsToTheGroundBeyond()
    {
        var row = BuildGap(landingHasFloor: true);

        Play(new MinerInput { Up = true, Horizontal = 1 }, FrameSeconds);
        Play(default, MinerControlSystem.JumpDuration);

        Assert.That(_miner.Column, Is.EqualTo(GapColumn + 1));
        Assert.That(_miner.Row, Is.EqualTo(row), "landed on the far side, not in the hole");
        Assert.That(_miner.IsMoving, Is.False);
    }

    [Test]
    public void Control_JumpMidAir_RisesAboveTheRow()
    {
        var row = BuildGap(landingHasFloor: true);
        var groundY = Layout.TileToWorld(GapColumn - 1, row).Y;

        Play(new MinerInput { Up = true, Horizontal = 1 }, MinerControlSystem.JumpDuration / 2f);

        Assert.That(_miner.Motion, Is.EqualTo(MinerMotion.Jumping));
        Assert.That(_position.Value.Y, Is.LessThan(groundY - 1f));
    }

    [Test]
    public void Control_JumpTowardsAnotherHole_FallsIntoIt()
    {
        var row = BuildGap(landingHasFloor: false);

        Play(new MinerInput { Up = true, Horizontal = 1 }, FrameSeconds);
        Play(default, MinerControlSystem.JumpDuration);
        Play(default, .5f);

        Assert.That(_miner.Column, Is.EqualTo(GapColumn + 1));
        Assert.That(_miner.Row, Is.GreaterThan(row));
    }

    [Test]
    public void Control_UpAndRightWithRockBeyondTheGap_WalksIntoTheGap()
    {
        var row = BuildGap(landingHasFloor: true);
        _mine.At(GapColumn + 1, row) = new TileCell { Type = TileType.Stone };

        Play(new MinerInput { Up = true, Horizontal = 1 }, MinerControlSystem.WalkDuration + .3f);

        Assert.That(_miner.Column, Is.EqualTo(GapColumn));
        Assert.That(_miner.Row, Is.GreaterThan(row), "no room to land: walked off and fell");
    }

    private const int GapColumn = 6;

    /// <summary>
    /// A tunnel 60 m down with the miner standing left of a one-tile-wide hole at <see cref="GapColumn"/> (3 deep),
    /// and air beyond it with or without a floor. Returns the tunnel's row.
    /// </summary>
    private int BuildGap(bool landingHasFloor)
    {
        var row = Mine.SurfaceRow + 60;
        for (var column = GapColumn - 2; column <= GapColumn + 2; column++)
        {
            _mine.At(column, row - 1) = new TileCell { Type = TileType.Air };
            _mine.At(column, row) = new TileCell { Type = TileType.Air };
            _mine.At(column, row + 1) = new TileCell { Type = TileType.Stone };
        }

        for (var depth = 1; depth <= 3; depth++)
            _mine.At(GapColumn, row + depth) = new TileCell { Type = TileType.Air };
        _mine.At(GapColumn, row + 4) = new TileCell { Type = TileType.Stone };
        if (!landingHasFloor)
            _mine.At(GapColumn + 1, row + 1) = new TileCell { Type = TileType.Air };

        PlaceMiner(GapColumn - 1, row);
        return row;
    }

    private void PlaceMiner(int column, int row)
    {
        _miner = new Miner { Column = column, Row = row, FacingX = 1, Ladders = _miner.Ladders };
        _position = new Position(Layout.TileToWorld(column, row));
    }

    /// <summary>Sets the shaft column's tiles from row <paramref name="from"/> to <paramref name="to"/>.</summary>
    private void FillShaft(TileType type, int from, int to)
    {
        for (var row = from; row <= to; row++)
            _mine.At(Mine.ShaftColumn, row) = new TileCell { Type = type };
    }

    /// <summary>Air with a ladder in every shaft tile from the surface down to <paramref name="bottom"/>.</summary>
    private void LadderShaft(int bottom)
    {
        for (var row = Mine.SurfaceRow; row <= bottom; row++)
            _mine.At(Mine.ShaftColumn, row) = new TileCell { Type = TileType.Air, HasLadder = true };
        _mine.At(Mine.ShaftColumn, bottom + 1) = new TileCell { Type = TileType.Stone };
    }

    /// <summary>Plays frames with the input until the condition holds; returns the seconds it took.</summary>
    private float SecondsUntil(MinerInput input, Func<bool> done, float limit)
    {
        var elapsed = 0f;
        while (!done() && elapsed < limit)
        {
            Play(input, FrameSeconds);
            elapsed += FrameSeconds;
        }

        Assert.That(done(), Is.True, "condition reached within the limit");
        return elapsed;
    }

    private void Play(MinerInput input, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += FrameSeconds)
        {
            _control.Control(ref _miner, ref _position, input, FrameSeconds, _commands);
            _motion.Move(ref _miner, ref _position, FrameSeconds, _commands);
            _commands.Clear();
        }
    }
}
