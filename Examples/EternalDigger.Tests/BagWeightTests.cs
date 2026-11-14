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

public class BagWeightTests
{
    private const float FrameSeconds = 1f / 60f;

    [Test]
    public void BagWeight_EachKind_UsesItsWeight()
    {
        var miner = new Miner { CopperOre = 3, SilverOre = 2, GoldOre = 4, GemOre = 1, Fossils = 1, Relics = 1 };

        Assert.That(Shop.BagWeight(miner), Is.EqualTo(3 + 2 + 8 + 2 + 3 + 3));
    }

    [TestCase(20, 0)]
    [TestCase(21, 1)]
    [TestCase(30, 1)]
    [TestCase(31, 2)]
    public void WeightSteps_OverTheCapacity_OnePerTenOrPart(int copper, int steps) =>
        Assert.That(Shop.WeightSteps(new Miner { CopperOre = copper }), Is.EqualTo(steps));

    [Test]
    public void BagCapacity_BagTier_AddsTenEach()
    {
        var miner = new Miner { CopperOre = 40, BagTier = 2 };

        Assert.That(Shop.BagCapacity(miner), Is.EqualTo(40));
        Assert.That(Shop.IsHeavy(miner), Is.False);
    }

    [Test]
    public void ClimbLoad_VeryHeavy_IsCapped()
    {
        Assert.That(Shop.ClimbLoad(new Miner { CopperOre = 25 }), Is.EqualTo(1.2f).Within(.0001f));
        Assert.That(Shop.ClimbLoad(new Miner { CopperOre = 1000 }), Is.EqualTo(Shop.MaxClimbSlowdown));
    }

    [Test]
    public void Control_HeavyBag_ClimbsSlowerButWalksTheSame()
    {
        var light = ClimbSeconds(new Miner());
        var heavy = ClimbSeconds(new Miner { GoldOre = 20 });

        Assert.That(heavy, Is.GreaterThan(light * 1.3f), "2 steps over: 40% slower per tile");
        Assert.That(WalkSeconds(new Miner { GoldOre = 20 }), Is.EqualTo(WalkSeconds(new Miner())).Within(FrameSeconds));
    }

    [Test]
    public void LoadFactor_HeavyWhileClimbingUp_BurnsMoreOil()
    {
        var climbing = new Miner
        {
            GoldOre = 20, Motion = MinerMotion.Climbing, Row = 30, MoveFrom = new Vector2(5, 31), MoveDuration = 1f
        };
        var standing = climbing with { Motion = MinerMotion.Idle, MoveFrom = new Vector2(5, 30) };

        Assert.That(LampSystem.LoadFactor(climbing), Is.EqualTo(2f).Within(.0001f));
        Assert.That(LampSystem.LoadFactor(standing), Is.EqualTo(1f));
    }

    private static float ClimbSeconds(Miner miner)
    {
        var mine = new Mine(4);
        var bottom = Mine.SurfaceRow + 12;
        for (var row = Mine.SurfaceRow; row <= bottom; row++)
            mine.At(Mine.ShaftColumn, row) = new TileCell { Type = TileType.Air, HasLadder = true };
        mine.At(Mine.ShaftColumn, bottom + 1) = new TileCell { Type = TileType.Stone };
        miner.Column = Mine.ShaftColumn;
        miner.Row = bottom;
        miner.MoveFrom = new Vector2(miner.Column, bottom);
        return Run(mine, miner, new MinerInput { Up = true }, m => m.Row <= bottom - 10 && !m.IsMoving);
    }

    private static float WalkSeconds(Miner miner)
    {
        var mine = new Mine(4);
        miner.Column = 2;
        miner.Row = Layout.GroundRow;
        miner.MoveFrom = new Vector2(2, Layout.GroundRow);
        return Run(mine, miner, new MinerInput { Horizontal = 1 }, m => m.Column >= 12 && !m.IsMoving);
    }

    private static float Run(Mine mine, Miner miner, MinerInput input, Func<Miner, bool> done)
    {
        var effects = new EffectSpawner(new Random(4), new ScreenShake());
        var control = new MinerControlSystem(mine, new Elevator(), effects);
        var motion = new MinerMotionSystem(mine, effects);
        var commands = new CommandBuffer();
        miner.FacingX = 1;
        var position = new Position(Layout.TileToWorld(miner.Column, miner.Row));
        var seconds = 0f;
        while (!done(miner) && seconds < 30f)
        {
            control.Control(ref miner, ref position, input, FrameSeconds, commands);
            motion.Move(ref miner, ref position, FrameSeconds, commands);
            commands.Clear();
            seconds += FrameSeconds;
        }

        return seconds;
    }
}
