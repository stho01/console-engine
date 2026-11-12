using System;
using NUnit.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Tests;

public class StageTests
{
    [Test]
    public void Parse_RowsOfDifferentWidths_Throws() =>
        Assert.Throws<FormatException>(() => Stage.Parse(["###", "##"], out _));

    [Test]
    public void Parse_UnknownCharacter_Throws() =>
        Assert.Throws<FormatException>(() => Stage.Parse(["#?#"], out _));

    [Test]
    public void Parse_Symbols_BecomeTilesChannelsAndSpawns()
    {
        var stage = Stage.Parse(["#%^Xbq", "C3YGSo"], out var spawns);

        Assert.That(stage.KindAt(1, 0), Is.EqualTo(TileKind.Cracked));
        Assert.That(stage.KindAt(2, 0), Is.EqualTo(TileKind.Spikes));
        Assert.That(stage.KindAt(3, 0), Is.EqualTo(TileKind.Exit));
        Assert.That((stage.KindAt(4, 0), stage.ChannelAt(4, 0)), Is.EqualTo((TileKind.Door, 1)));
        Assert.That((stage.KindAt(5, 0), stage.ChannelAt(5, 0)), Is.EqualTo((TileKind.Bridge, 1)));
        Assert.That((stage.KindAt(0, 1), stage.ChannelAt(0, 1)), Is.EqualTo((TileKind.Plate, 2)));
        Assert.That((stage.KindAt(1, 1), stage.ChannelAt(1, 1)), Is.EqualTo((TileKind.Lever, 2)));
        Assert.That(spawns, Is.EqualTo(new[]
        {
            new Spawn(ActorKind.Ylva, 2, 1), new Spawn(ActorKind.Gorm, 3, 1), new Spawn(ActorKind.Sigrid, 4, 1),
            new Spawn(ActorKind.Crate, 5, 1)
        }));
    }

    [Test]
    public void IsSolid_OutsideTheMap_IsTrue()
    {
        var stage = Stage.Parse(["..."], out _);

        Assert.That(stage.IsSolid(-1, 0), Is.True);
        Assert.That(stage.IsSolid(0, 1), Is.True);
        Assert.That(stage.IsSolid(0, 0), Is.False);
    }

    [Test]
    public void UpdateSwitches_LeverOn_OpensDoorAndExtendsBridge()
    {
        var stage = Stage.Parse(["1ap"], out _);
        Assert.That(stage.IsSolid(1, 0), Is.True, "door starts closed");
        Assert.That(stage.IsSolid(2, 0), Is.False, "bridge starts retracted");

        stage.ToggleLever(0, 0);
        stage.UpdateSwitches([]);

        Assert.That(stage.IsSolid(1, 0), Is.False);
        Assert.That(stage.IsSolid(2, 0), Is.True);
    }

    [Test]
    public void UpdateSwitches_CrateOnPlate_HoldsTheDoorOpenUntilItLeaves()
    {
        var stage = Stage.Parse(["Aa", "##"], out _);
        Actor[] crate = [Heroes.Create(new Spawn(ActorKind.Crate, 0, 0))];

        stage.UpdateSwitches(crate);
        Assert.That(stage.IsOn(0, 0), Is.True, "plate pressed");
        Assert.That(stage.IsSolid(1, 0), Is.False);

        crate[0].Min.Y = -5f;
        stage.UpdateSwitches(crate);
        Assert.That(stage.IsSolid(1, 0), Is.True);
    }

    [Test]
    public void UpdateSwitches_DoorClosingOnAHero_StaysOpenUntilTheHeroLeaves()
    {
        var stage = Stage.Parse(["1a.", "###"], out _);
        stage.ToggleLever(0, 0);
        stage.UpdateSwitches([]);
        Actor[] hero = [Heroes.Create(new Spawn(ActorKind.Gorm, 1, 0))];

        stage.ToggleLever(0, 0);
        stage.UpdateSwitches(hero);
        Assert.That(stage.IsSolid(1, 0), Is.False, "blocked by the hero");

        hero[0].Min.X = 2.1f;
        stage.UpdateSwitches(hero);
        Assert.That(stage.IsSolid(1, 0), Is.True);
    }

    [Test]
    public void Smash_OnlyCrackedStone()
    {
        var stage = Stage.Parse(["#%"], out _);

        Assert.That(stage.Smash(0, 0), Is.False);
        Assert.That(stage.Smash(1, 0), Is.True);
        Assert.That(stage.IsSolid(1, 0), Is.False);
    }
}
