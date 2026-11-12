using System;
using System.IO;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Saving;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// The deep camp: buying it at the surface, the tiles it digs, its prices and the bag sale, and trading at its counter
/// through <see cref="InteractionSystem.Interact"/> with the miner's rules in the same frames.
/// </summary>
public class DeepCampTests
{
    private const int Seed = 13;

    private Mine _mine;
    private ShopCounter _counter;
    private ShopCounter _campCounter;
    private InteractionSystem _interaction;
    private MinerControlSystem _control;
    private CommandBuffer _commands;

    [SetUp]
    public void SetUp()
    {
        _mine = new Mine(Seed);
        var elevator = new Elevator();
        var effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        var saver = new GameSaver(Path.Combine(Path.GetTempPath(), "unused.dat"), Seed, _mine, elevator);
        _counter = new ShopCounter();
        _campCounter = new ShopCounter(ShopCounter.CampItems);
        _interaction = new InteractionSystem(_mine, elevator, _counter, effects, saver, _campCounter);
        _control = new MinerControlSystem(_mine, elevator, effects);
        _commands = new CommandBuffer();
    }

    [Test]
    public void TryBuy_SurfaceCounter_BuysTheCampOnce()
    {
        var miner = new Miner { Gold = DeepCamp.Price * 2 };

        Assert.That(Shop.TryBuy(ShopItem.DeepCamp, ref miner), Is.True);
        Assert.That(Shop.TryBuy(ShopItem.DeepCamp, ref miner), Is.False);
        Assert.That(miner.HasDeepCamp, Is.True);
        Assert.That(miner.Gold, Is.EqualTo(DeepCamp.Price));
        Assert.That(Shop.Price(ShopItem.DeepCamp, miner), Is.EqualTo(Shop.SoldOut));
    }

    [Test]
    public void Interact_BuyTheCampAtTheSurface_DigsItOutWithAFloor()
    {
        _mine.At(DeepCamp.FirstColumn, DeepCamp.Row) = new TileCell { Type = TileType.Gold };
        _mine.At(DeepCamp.LastColumn, DeepCamp.Row + 1) = new TileCell { Type = TileType.Air };
        var miner = AtSurfaceCounter(DeepCamp.Price);
        _counter.Select(ShopItem.DeepCamp);

        _interaction.Interact(ref miner, new MinerInput { UsePressed = true }, _commands);

        Assert.That(miner.HasDeepCamp, Is.True);
        Assert.That(miner.GoldOre, Is.EqualTo(1), "ore dug out by the builders goes into the bag");
        for (var column = DeepCamp.FirstColumn; column <= DeepCamp.LastColumn; column++)
        {
            Assert.That(_mine.TypeAt(column, DeepCamp.Row), Is.EqualTo(TileType.Air));
            Assert.That(_mine.TypeAt(column, DeepCamp.Row - 1), Is.EqualTo(TileType.Air));
            Assert.That(_mine.IsSolid(column, DeepCamp.Row + 1), Is.True, $"floor under column {column}");
        }
    }

    [Test]
    public void Sale_FullBag_PaysSeventyPercent()
    {
        var miner = new Miner { GoldOre = 10, HasDeepCamp = true };

        Assert.That(DeepCamp.Sale(miner), Is.EqualTo(700));
        Assert.That(DeepCamp.SellBag(ref miner), Is.EqualTo(700));
        Assert.That(miner.Gold, Is.EqualTo(700));
        Assert.That(miner.OreCount, Is.Zero);
    }

    [Test]
    public void CampPrice_Supplies_AreMarkedUp()
    {
        var miner = new Miner { Oil = LampOil.TankSize - 10f };

        Assert.That(Shop.CampPrice(ShopItem.Ladder, miner), Is.EqualTo(15));
        Assert.That(Shop.CampPrice(ShopItem.OilFlask, miner), Is.EqualTo(90));
        Assert.That(Shop.CampPrice(ShopItem.LampOil, miner), Is.EqualTo(30));
        Assert.That(Shop.CampPrice(ShopItem.Blasting, miner), Is.EqualTo(Shop.SoldOut), "no kit, no sticks");
        miner.HasBlastingKit = true;
        Assert.That(Shop.CampPrice(ShopItem.Blasting, miner), Is.EqualTo(150));
        Assert.That(Shop.CampPrice(ShopItem.Shovel, miner), Is.EqualTo(Shop.SoldOut), "no upgrades at the camp");
    }

    [Test]
    public void Interact_AtTheCampCounter_SellsTheBagFirst()
    {
        var miner = AtCamp();
        miner.SilverOre = 5;

        _interaction.Interact(ref miner, new MinerInput { UsePressed = true }, _commands);

        Assert.That(miner.Gold, Is.EqualTo(140));
        Assert.That(miner.SilverOre, Is.Zero);
    }

    [Test]
    public void Interact_DownThenUseAtTheCampCounter_BuysALadderAtTheMarkup()
    {
        var miner = AtCamp();
        miner.Gold = 100;

        _interaction.Interact(ref miner, new MinerInput { DownPressed = true, Down = true }, _commands);
        _interaction.Interact(ref miner, new MinerInput { UsePressed = true }, _commands);

        Assert.That(_campCounter.Selected, Is.EqualTo(ShopItem.Ladder));
        Assert.That(miner.Ladders, Is.EqualTo(1));
        Assert.That(miner.Gold, Is.EqualTo(85));
    }

    [Test]
    public void Control_DownAtTheCampCounter_DoesNotDigTheFloor()
    {
        var miner = AtCamp();
        var position = new Position(Layout.TileToWorld(miner.Column, miner.Row));

        for (var frame = 0; frame < 60; frame++)
            _control.Control(ref miner, ref position, new MinerInput { Down = true, Use = true }, 1f / 60f, _commands);

        Assert.That(_mine.At(miner.Column, DeepCamp.Row + 1).Damage, Is.Zero);
        Assert.That(miner.PlantHold, Is.Zero, "E at the counter doesn't light a fuse");
    }

    [Test]
    public void AddLantern_BuiltCamp_LightsTheCamp()
    {
        var light = new LightMap(Mine.Columns, 30);
        light.Compute(_mine, DeepCamp.Row - 10, 30, new Vector2(2.5f, DeepCamp.Row - 8.5f), Vector2.UnitX);
        Assert.That(light.Get(DeepCamp.FirstColumn, DeepCamp.Row), Is.EqualTo(LightMap.Dark));

        light.AddLantern(_mine, DeepCamp.LanternCenter, DeepCamp.LanternRadius);

        Assert.That(light.Get(DeepCamp.FirstColumn, DeepCamp.Row), Is.GreaterThanOrEqualTo(LightMap.Lit));
        Assert.That(_mine.At(DeepCamp.LastColumn, DeepCamp.Row).Seen, Is.True);
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static Miner AtSurfaceCounter(int gold) => new()
    {
        Column = Layout.ShopFirstColumn, Row = Layout.GroundRow,
        MoveFrom = new Vector2(Layout.ShopFirstColumn, Layout.GroundRow), FacingX = 1, Gold = gold
    };

    /// <summary>A built camp with the miner standing at its counter.</summary>
    private Miner AtCamp()
    {
        var miner = new Miner
        {
            Column = DeepCamp.FirstColumn + 1, Row = DeepCamp.Row,
            MoveFrom = new Vector2(DeepCamp.FirstColumn + 1, DeepCamp.Row), FacingX = 1, HasDeepCamp = true,
            HasBlastingKit = true, Dynamite = 2, LampDirection = Vector2.UnitY
        };
        DeepCamp.Build(_mine, ref miner);
        Shop.EmptyBag(ref miner);
        return miner;
    }
}
