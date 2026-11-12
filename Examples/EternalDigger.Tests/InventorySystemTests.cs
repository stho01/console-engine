using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using EternalDigger.Tests.Fakes;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>The TAB screen: it pauses the simulation, and shows what the miner carries.</summary>
public class InventorySystemTests
{
    private World _world;
    private SystemGroup _simulation;
    private RenderConsole _console;
    private InventorySystem _inventory;
    private ParticleLifetimeSystem _someSystem;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _simulation = new SystemGroup(_world);
        _someSystem = _simulation.Add(new ParticleLifetimeSystem());
        _console = new RenderConsole(new FakeConsoleHandler(Layout.ScreenWidth, Layout.ScreenHeight));
        var presentation = new SystemGroup(_world);
        _inventory = presentation.Add(new InventorySystem(new Canvas(_console), new Elevator(), _simulation));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Toggle_Open_PausesTheSimulation()
    {
        _inventory.Toggle();

        Assert.That(_inventory.IsOpen, Is.True);
        Assert.That(_someSystem.Enabled, Is.False);
    }

    [Test]
    public void Toggle_Twice_ResumesTheSimulation()
    {
        _inventory.Toggle();
        _inventory.Toggle();

        Assert.That(_inventory.IsOpen, Is.False);
        Assert.That(_someSystem.Enabled, Is.True);
    }

    [Test]
    public void Draw_Miner_ListsTheInventory()
    {
        var miner = new Miner
        {
            Gold = 555, Ladders = 7, CopperOre = 3, GemOre = 2, OilFlasks = 1, Oil = 50f, PickaxeTier = 2,
            FossilsFound = 4
        };

        _inventory.Draw(miner);

        var lines = _inventory.Lines;
        Assert.That(lines[1], Does.Contain("555g").And.Contain("LADDERS   7"));
        Assert.That(lines[2], Does.Contain("50%").And.Contain("δ1/5"));
        Assert.That(lines[6], Does.Contain("copper").And.Contain("45g"));
        Assert.That(lines[9], Does.Contain("gem").And.Contain(" 2 ×"));
        Assert.That(lines[15], Does.Contain(Upgrades.PickaxeName + "  ●●○"));
        Assert.That(lines[17], Does.Contain("§4 fossils"));
    }

    [Test]
    public void Draw_ValueChanged_RebuildsTheLines()
    {
        var miner = new Miner { Gold = 10 };
        _inventory.Draw(miner);

        miner.Gold = 20;
        _inventory.Draw(miner);

        Assert.That(_inventory.Lines[1], Does.Contain("20g"));
    }

    [Test]
    public void Draw_Panel_IsFramedAndTitled()
    {
        _inventory.Draw(new Miner());

        var found = false;
        for (var y = 0; y < Layout.ScreenHeight && !found; y++)
        for (var x = 0; x < Layout.ScreenWidth && !found; x++)
            found = _console.GetCharAt(x, y) == '╔' && _console.GetCharAt(x + 3, y) == 'I';

        Assert.That(found, Is.True, "a ╔ corner with the INVENTORY title next to it");
    }
}
