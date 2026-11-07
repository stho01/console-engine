using System;
using System.IO;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Saving;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>The counter's selection, and buying at it through <see cref="InteractionSystem.Interact"/>.</summary>
public class ShopCounterTests
{
    private ShopCounter _counter;
    private InteractionSystem _interaction;
    private CommandBuffer _commands;
    private Miner _miner;

    [SetUp]
    public void SetUp()
    {
        var mine = new Mine(3);
        var elevator = new Elevator();
        _counter = new ShopCounter();
        var saver = new GameSaver(Path.Combine(Path.GetTempPath(), "unused.dat"), 3, mine, elevator);
        _interaction = new InteractionSystem(mine, elevator, _counter,
            new EffectSpawner(new Random(3), new ScreenShake()), saver);
        _commands = new CommandBuffer();
        _miner = new Miner
        {
            Column = Layout.ShopFirstColumn, Row = Layout.GroundRow,
            MoveFrom = new Vector2(Layout.ShopFirstColumn, Layout.GroundRow), FacingX = 1
        };
    }

    [Test]
    public void Selected_AtTheStart_IsLadders() => Assert.That(_counter.Selected, Is.EqualTo(ShopItem.Ladder));

    [Test]
    public void NextAndPrevious_PastTheEnds_WrapAround()
    {
        _counter.Previous();
        Assert.That(_counter.Selected, Is.EqualTo(ShopCounter.Items[^1]));

        _counter.Next();
        Assert.That(_counter.Selected, Is.EqualTo(ShopCounter.Items[0]));
    }

    [Test]
    public void Interact_PressE_BuysTheSelectedItem()
    {
        _miner.Gold = Shop.LadderPrice;

        Press(new MinerInput { UsePressed = true });

        Assert.That(_miner.Ladders, Is.EqualTo(1));
        Assert.That(_miner.Gold, Is.Zero);
    }

    [Test]
    public void Interact_DownThenE_BuysTheBlastingKit()
    {
        _miner.Gold = Shop.BlastingKitPrice;

        Press(new MinerInput { DownPressed = true });
        Press(new MinerInput { UsePressed = true });

        Assert.That(_counter.Selected, Is.EqualTo(ShopItem.Blasting));
        Assert.That(_miner.HasBlastingKit, Is.True);
        Assert.That(_miner.Ladders, Is.Zero);
    }

    [Test]
    public void Interact_UpDownAwayFromTheCounter_KeepsTheSelection()
    {
        _miner.Column = Layout.SpawnColumn;

        Press(new MinerInput { DownPressed = true });

        Assert.That(_counter.Selected, Is.EqualTo(ShopItem.Ladder));
    }

    [Test]
    public void Interact_TooPoor_BuysNothing()
    {
        _miner.Gold = Shop.LadderPrice - 1;

        Press(new MinerInput { UsePressed = true });

        Assert.That(_miner.Ladders, Is.Zero);
        Assert.That(_miner.Gold, Is.EqualTo(Shop.LadderPrice - 1));
    }

    [Test]
    public void Name_BlastingSlot_IsTheKitThenDynamite()
    {
        Assert.That(ShopCounter.Name(ShopItem.Blasting, _miner), Is.EqualTo(Shop.BlastingKitName));
        _miner.HasBlastingKit = true;
        Assert.That(ShopCounter.Name(ShopItem.Blasting, _miner), Is.EqualTo(Shop.DynamiteName));
        Assert.That(Shop.Price(ShopItem.Blasting, _miner), Is.EqualTo(Shop.DynamitePrice));
    }

    private void Press(MinerInput input)
    {
        _interaction.Interact(ref _miner, input, _commands);
        _commands.Clear();
    }
}
