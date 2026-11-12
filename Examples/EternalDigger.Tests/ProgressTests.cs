using System;
using System.IO;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Saving;
using EternalDigger.Systems;
using EternalDigger.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// Surface progression: the depth record, gold earned and the house levels, the tool shop that opens below the wall,
/// and the surface drawn from it.
/// </summary>
public class ProgressTests
{
    private const int Seed = 17;

    [TestCase(0, 0)]
    [TestCase(Progress.FlagGold - 1, 0)]
    [TestCase(Progress.FlagGold, 1)]
    [TestCase(Progress.LitWindowsGold, 2)]
    [TestCase(Progress.SecondFloorGold, 3)]
    [TestCase(1_000_000, 3)]
    public void HouseLevel_GoldEarned_FollowsTheMilestones(int goldEarned, int level) =>
        Assert.That(Progress.HouseLevel(goldEarned), Is.EqualTo(level));

    [Test]
    public void Track_Deeper_RaisesTheRecordButNeverLowersIt()
    {
        var mine = new Mine(Seed);
        var miner = new Miner { Column = 5, Row = Mine.SurfaceRow + 40 };

        Progress.Track(ref miner, mine);
        miner.Row = Mine.SurfaceRow + 10;
        Progress.Track(ref miner, mine);

        Assert.That(miner.DepthRecord, Is.EqualTo(40));
    }

    [Test]
    public void Track_BelowTheWall_OpensTheToolShopOnce()
    {
        var mine = new Mine(Seed);
        var row = Mine.SurfaceRow + mine.WallBottomDepth(5) + 1;
        var miner = new Miner { Column = 5, Row = row - 2 };

        Assert.That(Progress.Track(ref miner, mine), Is.False, "still in the wall");
        miner.Row = row;
        Assert.That(Progress.Track(ref miner, mine), Is.True);
        Assert.That(Progress.Track(ref miner, mine), Is.False, "only the first time");
        Assert.That(Progress.IsToolShopOpen(miner), Is.True);
    }

    [Test]
    public void Deliver_AndCampSale_CountAsGoldEarned()
    {
        var miner = new Miner { GoldOre = 10 };
        Shop.Deliver(ref miner);
        miner.GoldOre = 10;
        miner.HasDeepCamp = true;
        DeepCamp.SellBag(ref miner);
        miner.Gold -= 500;

        Assert.That(miner.GoldEarned, Is.EqualTo(1000 + 700), "spending doesn't lower it");
    }

    [Test]
    public void Interact_ClosedToolShop_SellsNothing()
    {
        var (interaction, _) = Interaction();
        var miner = AtToolShop(gold: 1000, open: false);

        interaction.Interact(ref miner, new MinerInput { UsePressed = true }, new CommandBuffer());

        Assert.That(miner.Gold, Is.EqualTo(1000));
    }

    [Test]
    public void Interact_OpenToolShop_SellsBundles()
    {
        var (interaction, commands) = Interaction();
        var miner = AtToolShop(gold: 1000, open: true);
        miner.HasBlastingKit = true;

        interaction.Interact(ref miner, new MinerInput { UsePressed = true }, commands);
        interaction.Interact(ref miner, new MinerInput { DownPressed = true }, commands);
        interaction.Interact(ref miner, new MinerInput { UsePressed = true }, commands);

        Assert.That(miner.Dynamite, Is.EqualTo(Shop.DynamiteBundleSize));
        Assert.That(miner.Ladders, Is.EqualTo(Shop.LadderBundleSize));
        Assert.That(miner.Gold, Is.EqualTo(1000 - Shop.DynamiteBundlePrice - Shop.LadderBundlePrice));
    }

    [Test]
    public void TryBuy_DynamiteBundleWithoutTheKit_IsSoldOut()
    {
        var miner = new Miner { Gold = 1000 };

        Assert.That(Shop.Price(ShopItem.DynamiteBundle, miner), Is.EqualTo(Shop.SoldOut));
        Assert.That(Shop.TryBuy(ShopItem.DynamiteBundle, ref miner), Is.False);
    }

    [Test]
    public void HouseRenderSystem_Progress_ShowsTrophiesRecordFlagAndToolsmith()
    {
        using var world = new World();
        var console = new RenderConsole(new FakeConsoleHandler(Layout.ScreenWidth, Layout.ScreenHeight));
        var camera = new Camera { Position = new Vector2(0f, -Layout.HudHeight) };
        var group = new SystemGroup(world);
        group.Add(new HouseRenderSystem(new Canvas(console), camera));
        world.Create(new Miner
        {
            GoldEarned = Progress.SecondFloorGold, FossilsFound = 2, RelicsFound = 1, DepthRecord = 112,
            PassedTheWall = true
        });

        group.Update(0f);

        var screen = ReadScreen(console);
        Assert.That(screen, Does.Contain("¥§§"), "trophy case: relics first");
        Assert.That(screen, Does.Contain("RECORD 112m"));
        Assert.That(screen, Does.Contain("☺"), "the toolsmith");
        Assert.That(screen, Does.Not.Contain(HouseRenderSystem.ClosedText));
    }

    [Test]
    public void HouseRenderSystem_NewMine_ShowsAClosedToolShopAndNoRecord()
    {
        using var world = new World();
        var console = new RenderConsole(new FakeConsoleHandler(Layout.ScreenWidth, Layout.ScreenHeight));
        var group = new SystemGroup(world);
        group.Add(new HouseRenderSystem(new Canvas(console), new Camera { Position = new Vector2(0f, -2f) }));
        world.Create(new Miner());

        group.Update(0f);

        var screen = ReadScreen(console);
        Assert.That(screen, Does.Contain(HouseRenderSystem.ClosedText));
        Assert.That(screen, Does.Not.Contain("RECORD"));
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static (InteractionSystem, CommandBuffer) Interaction()
    {
        var mine = new Mine(Seed);
        var elevator = new Elevator();
        var saver = new GameSaver(Path.Combine(Path.GetTempPath(), "unused.dat"), Seed, mine, elevator);
        var interaction = new InteractionSystem(mine, elevator, new ShopCounter(),
            new EffectSpawner(new Random(Seed), new ScreenShake()), saver);
        return (interaction, new CommandBuffer());
    }

    private static Miner AtToolShop(int gold, bool open) => new()
    {
        Column = Layout.ToolShopColumn, Row = Layout.GroundRow,
        MoveFrom = new Vector2(Layout.ToolShopColumn, Layout.GroundRow), FacingX = 1, Gold = gold,
        PassedTheWall = open
    };

    private static string ReadScreen(RenderConsole console)
    {
        var text = new System.Text.StringBuilder();
        for (var y = 0; y < Layout.ScreenHeight; y++)
        {
            for (var x = 0; x < Layout.ScreenWidth; x++)
                text.Append(console.GetCharAt(x, y));
            text.Append('\n');
        }

        return text.ToString();
    }
}
