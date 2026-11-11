using System;
using System.IO;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Saving;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// The lift's rules, and frames of riding it: control, motion, the car and <c>E</c> played like the scene does.
/// </summary>
public class ElevatorTests
{
    private const float FrameSeconds = 1f / 60f;
    private const int Seed = 7;

    private Mine _mine;
    private Elevator _elevator;
    private MinerControlSystem _control;
    private MinerMotionSystem _motion;
    private ElevatorSystem _carry;
    private InteractionSystem _interaction;
    private CommandBuffer _commands;
    private Miner _miner;
    private Position _position;

    [SetUp]
    public void SetUp()
    {
        _mine = new Mine(Seed);
        _elevator = new Elevator();
        var effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        var saver = new GameSaver(Path.Combine(Path.GetTempPath(), "unused.dat"), Seed, _mine, _elevator);
        _control = new MinerControlSystem(_mine, _elevator, effects);
        _motion = new MinerMotionSystem(_mine, effects);
        _carry = new ElevatorSystem(_elevator);
        _interaction = new InteractionSystem(_mine, _elevator, new ShopCounter(), effects, saver);
        _commands = new CommandBuffer();
        PlaceMiner(Elevator.Column, Elevator.TopRow);
    }

    [Test]
    public void TryBuild_EnoughGold_DigsOutTheShaftAndCharges()
    {
        _miner.Gold = 1000;

        var first = _elevator.TryBuild(_mine, ref _miner);
        var second = _elevator.TryBuild(_mine, ref _miner);

        Assert.That(first, Is.EqualTo(ElevatorBuild.Built));
        Assert.That(second, Is.EqualTo(ElevatorBuild.Built));
        Assert.That(_miner.Gold, Is.EqualTo(1000 - Elevator.FirstSegmentPrice -
                                            (Elevator.FirstSegmentPrice + Elevator.SegmentPriceStep)));
        Assert.That(_elevator.Depth, Is.EqualTo(2 * Elevator.SegmentMetres));
        Assert.That(_elevator.BottomRow, Is.EqualTo(Mine.SurfaceRow + 20));
        for (var row = Mine.SurfaceRow + 1; row <= _elevator.BottomRow; row++)
            Assert.That(_mine.TypeAt(Elevator.Column, row), Is.EqualTo(TileType.Air), $"row {row}");
        Assert.That(_mine.IsSolid(Elevator.Column, _elevator.BottomRow + 1), Is.True, "rock under the bottom landing");
    }

    [Test]
    public void TryBuild_OreAndLaddersInTheShaft_BagsTheOreAndKeepsTheLadders()
    {
        _miner.Gold = Elevator.FirstSegmentPrice;
        _mine.At(Elevator.Column, Mine.SurfaceRow + 5) = new TileCell { Type = TileType.Gold };
        _mine.At(Elevator.Column, Mine.SurfaceRow + 3) = new TileCell { Type = TileType.Air, HasLadder = true };

        _elevator.TryBuild(_mine, ref _miner);

        Assert.That(_miner.GoldOre, Is.EqualTo(1));
        Assert.That(_mine.HasLadder(Elevator.Column, Mine.SurfaceRow + 3), Is.True);
    }

    [Test]
    public void TryBuild_TooPoor_ChangesNothing()
    {
        _miner.Gold = Elevator.FirstSegmentPrice - 1;
        var below = _mine.TypeAt(Elevator.Column, Mine.SurfaceRow + 4);

        Assert.That(_elevator.TryBuild(_mine, ref _miner), Is.EqualTo(ElevatorBuild.TooPoor));
        Assert.That(_elevator.IsBuilt, Is.False);
        Assert.That(_miner.Gold, Is.EqualTo(Elevator.FirstSegmentPrice - 1));
        Assert.That(_mine.TypeAt(Elevator.Column, Mine.SurfaceRow + 4), Is.EqualTo(below));
    }

    [Test]
    public void TryBuild_ObsidianInTheNextSegment_IsBlocked()
    {
        _miner.Gold = 10_000;
        _mine.At(Elevator.Column, Mine.SurfaceRow + 7) = new TileCell { Type = TileType.Obsidian };

        Assert.That(_elevator.CanBuildNext(_mine), Is.False);
        Assert.That(_elevator.TryBuild(_mine, ref _miner), Is.EqualTo(ElevatorBuild.Blocked));
        Assert.That(_elevator.IsBuilt, Is.False);
        Assert.That(_miner.Gold, Is.EqualTo(10_000));
    }

    [Test]
    public void TryBuild_DownToTheWall_StopsAboveIt()
    {
        _miner.Gold = int.MaxValue / 2;
        while (_elevator.TryBuild(_mine, ref _miner) == ElevatorBuild.Built)
        {
        }

        Assert.That(_elevator.Depth, Is.EqualTo(Tiles.WallDepth - Elevator.SegmentMetres));
    }

    [Test]
    public void Update_SentToTheBottom_TakesRideDurationPerTile()
    {
        Build(segments: 10);
        var tiles = _elevator.BottomRow - Elevator.TopRow;

        _elevator.Send(_elevator.BottomRow);
        var seconds = 0f;
        while (_elevator.IsCarMoving)
        {
            _elevator.Update(FrameSeconds);
            seconds += FrameSeconds;
        }

        Assert.That(seconds, Is.EqualTo(tiles * Elevator.RideDuration).Within(FrameSeconds));
    }

    [Test]
    public void Play_BoardAtTheTopAndRideDown_ArrivesAtTheBottomStillAboard()
    {
        Build(segments: 3);

        Press();
        Play(new MinerInput { Down = true }, FrameSeconds);
        Play(default, 2f);

        Assert.That(_miner.IsRiding, Is.True);
        Assert.That(_miner.Row, Is.EqualTo(_elevator.BottomRow));
        Assert.That(_position.Value.Y, Is.EqualTo(_elevator.BottomRow * Layout.TileHeight));
    }

    [Test]
    public void Play_RideUpAndStepOff_WalksOntoTheSurface()
    {
        Build(segments: 2);
        _elevator.Send(_elevator.BottomRow);
        _elevator.Update(10f);
        PlaceMiner(Elevator.Column, _elevator.BottomRow);
        Press();

        Play(new MinerInput { Up = true }, FrameSeconds);
        Play(default, 2f);
        Play(new MinerInput { Horizontal = -1 }, .5f);

        Assert.That(_miner.IsRiding, Is.False);
        Assert.That(_miner.Row, Is.EqualTo(Layout.GroundRow));
        Assert.That(_miner.Column, Is.LessThan(Elevator.Column));
    }

    [Test]
    public void Play_CallFromTheBottom_CarComesAndTakesTheMinerAboard()
    {
        Build(segments: 2);
        PlaceMiner(Elevator.Column, _elevator.BottomRow);

        Press();
        Assert.That(_miner.IsRiding, Is.False, "the car is still at the top");
        Play(default, 2f);

        Assert.That(_elevator.IsCarAt(_elevator.BottomRow), Is.True);
        Assert.That(_miner.IsRiding, Is.True);
    }

    [Test]
    public void Play_DownAtTheBottom_StepsOffAndDigs()
    {
        Build(segments: 1);
        _elevator.Send(_elevator.BottomRow);
        _elevator.Update(10f);
        PlaceMiner(Elevator.Column, _elevator.BottomRow);
        _mine.At(Elevator.Column, _elevator.BottomRow + 1) = new TileCell { Type = TileType.Stone };
        Press();

        Play(new MinerInput { Down = true }, FrameSeconds);

        Assert.That(_miner.IsRiding, Is.False);
        Assert.That(_mine.At(Elevator.Column, _elevator.BottomRow + 1).Damage, Is.EqualTo(1));
    }

    [Test]
    public void Play_RescueWhileRiding_LeavesTheCar()
    {
        Build(segments: 2);
        Press();
        Play(new MinerInput { Down = true }, FrameSeconds);

        Play(new MinerInput { Rescue = true }, MinerControlSystem.RescueDuration + .1f);

        Assert.That(_miner.IsRiding, Is.False);
        Assert.That(_miner.Column, Is.EqualTo(Layout.SpawnColumn));
    }

    [Test]
    public void Interact_AtTheLiftSign_BuildsASegment()
    {
        PlaceMiner(Layout.LiftSignFirstColumn, Layout.GroundRow);
        _miner.Gold = Elevator.FirstSegmentPrice;

        Press();

        Assert.That(_elevator.Segments, Is.EqualTo(1));
        Assert.That(_miner.Gold, Is.Zero);
    }

    [Test]
    public void Use_TapOnTheBottomLandingWithDynamite_CallsTheCar()
    {
        Build(2);
        PlaceMiner(Elevator.Column, _elevator.BottomRow);
        _miner.HasBlastingKit = true;
        _miner.Dynamite = 3;

        Frame(new MinerInput { Use = true, UsePressed = true });
        Assert.That(_elevator.CarTargetRow, Is.EqualTo(Elevator.TopRow), "nothing on the press");
        Frame(new MinerInput { UseReleased = true });

        Assert.That(_elevator.CarTargetRow, Is.EqualTo(_elevator.BottomRow));
        Assert.That(_miner.Dynamite, Is.EqualTo(3));
    }

    [Test]
    public void Use_HoldOnTheBottomLandingWithDynamite_PlantsBelowAndLeavesTheLift()
    {
        Build(2);
        PlaceMiner(Elevator.Column, _elevator.BottomRow);
        _mine.At(Elevator.Column, _elevator.BottomRow + 1) = new TileCell { Type = TileType.Obsidian };
        _miner.HasBlastingKit = true;
        _miner.Dynamite = 3;

        Frame(new MinerInput { Use = true, UsePressed = true, Down = true });
        for (var t = 0f; t < MinerControlSystem.PlantDuration + .05f; t += FrameSeconds)
            Frame(new MinerInput { Use = true, Down = true });
        Frame(new MinerInput { UseReleased = true });

        Assert.That(_miner.Dynamite, Is.EqualTo(2));
        Assert.That(_miner.LitDynamite, Is.EqualTo(1));
        Assert.That(_elevator.CarTargetRow, Is.EqualTo(Elevator.TopRow), "the hold didn't call the car");
    }

    [Test]
    public void Use_PressOnTheBottomLandingWithoutDynamite_CallsTheCarAtOnce()
    {
        Build(2);
        PlaceMiner(Elevator.Column, _elevator.BottomRow);

        Frame(new MinerInput { Use = true, UsePressed = true });

        Assert.That(_elevator.CarTargetRow, Is.EqualTo(_elevator.BottomRow));
        Assert.That(_miner.PlantHold, Is.Zero);
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>One frame in the scene's order: control, <c>E</c>, motion, the car.</summary>
    private void Frame(MinerInput input)
    {
        _control.Control(ref _miner, ref _position, input, FrameSeconds, _commands);
        _interaction.Interact(ref _miner, input, _commands);
        _motion.Move(ref _miner, ref _position, FrameSeconds, _commands);
        _carry.Carry(ref _miner, ref _position, _elevator.Update(FrameSeconds));
        _commands.Clear();
    }

    private void Build(int segments)
    {
        var gold = _miner.Gold;
        _miner.Gold = int.MaxValue / 2;
        for (var i = 0; i < segments; i++)
            _elevator.TryBuild(_mine, ref _miner);

        _miner.Gold = gold;
        _miner.CopperOre = _miner.SilverOre = _miner.GoldOre = 0;
    }

    private void PlaceMiner(int column, int row)
    {
        _miner = new Miner
        {
            Column = column, Row = row, MoveFrom = new Vector2(column, row), FacingX = 1, Gold = _miner.Gold
        };
        _position = new Position(Layout.TileToWorld(column, row));
    }

    private void Press()
    {
        _interaction.Interact(ref _miner, new MinerInput { UsePressed = true }, _commands);
        _commands.Clear();
    }

    private void Play(MinerInput input, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += FrameSeconds)
        {
            _control.Control(ref _miner, ref _position, input, FrameSeconds, _commands);
            _motion.Move(ref _miner, ref _position, FrameSeconds, _commands);
            _carry.Carry(ref _miner, ref _position, _elevator.Update(FrameSeconds));
            _commands.Clear();
        }
    }
}
