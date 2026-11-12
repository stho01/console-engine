using System.Linq;
using NUnit.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Tests;

/// <summary>
/// Every level is well formed, fits on screen and can be solved: each level is played through here with the moves
/// a player would make (the intended solution, documented in the level's hint and in the docs).
/// </summary>
public class LevelsTests
{
    private static Level[] AllLevels => Levels.All;

    [TestCaseSource(nameof(AllLevels))]
    public void Level_Parses_FitsOnScreenWithEachHeroOnce(Level level)
    {
        var stage = Stage.Parse(level.Rows, out var spawns);

        Assert.That(stage.Columns, Is.LessThanOrEqualTo(Layout.MaxStageColumns));
        Assert.That(stage.Rows, Is.LessThanOrEqualTo(Layout.MaxStageRows));
        foreach (var hero in Heroes.Order)
            Assert.That(spawns.Count(spawn => spawn.Kind == hero), Is.EqualTo(1), hero.ToString());
        Assert.That(level.Hint.Length, Is.LessThanOrEqualTo(Layout.ScreenWidth - 2));
    }

    [Test]
    public void TheShore_YlvaPullsTheLever_AllReachTheExit()
    {
        var party = new Party(Levels.All[0].Rows);

        party.WalkTo(ActorKind.Ylva, 9.4f);
        party.Jump(ActorKind.Ylva, 1);
        party.WalkTo(ActorKind.Ylva, 11.5f);
        party.Use(ActorKind.Ylva);
        party.WalkTo(ActorKind.Ylva, 26.5f);
        party.WalkTo(ActorKind.Gorm, 25.5f);
        party.WalkTo(ActorKind.Sigrid, 27.5f);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Won));
    }

    [Test]
    public void TheShore_WithoutTheLever_DoorStopsGorm()
    {
        var party = new Party(Levels.All[0].Rows);

        party.WalkTo(ActorKind.Gorm, 25.5f);

        Assert.That(Party.CenterX(party.Get(ActorKind.Gorm)), Is.LessThan(20f));
    }

    [Test]
    public void Rockslide_GormSmashesAndPushes_AllClimbTheCrate()
    {
        var party = new Party(Levels.All[1].Rows);

        party.WalkTo(ActorKind.Gorm, 15f);
        party.Use(ActorKind.Gorm);
        party.WalkTo(ActorKind.Gorm, 22f);
        Assert.That(party.Crate().Min.X, Is.EqualTo(21f).Within(.01f));

        foreach (var hero in Heroes.Order)
        {
            party.WalkTo(hero, 20.4f);
            party.Jump(hero, 1);
            party.Jump(hero, 1);
            party.WalkTo(hero, 25.5f);
        }

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Won));
    }

    [Test]
    public void TheGorge_SigridGlidesOntoThePlate_OthersCrossTheBridge()
    {
        var party = new Party(Levels.All[2].Rows);

        party.WalkTo(ActorKind.Sigrid, 12.5f, glide: true);
        Assert.That(party.Stage.IsChannelOn(0), Is.True, "plate pressed");
        party.WalkTo(ActorKind.Ylva, 24.5f);
        party.WalkTo(ActorKind.Gorm, 25.5f);

        party.WalkTo(ActorKind.Sigrid, 15.5f);
        for (var step = 0; step < 5; step++)
            party.Jump(ActorKind.Sigrid, 1);
        party.WalkTo(ActorKind.Sigrid, 26.5f);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Won));
    }

    [Test]
    public void TheGorge_GormWalksOffTheCliff_IsKnockedOut()
    {
        var party = new Party(Levels.All[2].Rows) { FailOnKnockout = false };

        party.WalkTo(ActorKind.Gorm, 14f);

        Assert.That(party.Result, Is.EqualTo(StepResult.Failed(ActorKind.Gorm, FailCause.Fell)));
    }

    [Test]
    public void Counterweight_CrateHoldsThePlate_YlvaClimbsGorm()
    {
        var party = new Party(Levels.All[3].Rows);

        party.WalkTo(ActorKind.Gorm, 15.2f);
        Assert.That(party.Crate().Min, Is.EqualTo(new Microsoft.Xna.Framework.Vector2(9f, 10f)));
        Assert.That(party.Stage.IsChannelOn(0), Is.True, "crate on the plate");

        party.WalkTo(ActorKind.Ylva, 15.2f);
        party.Jump(ActorKind.Ylva);
        Assert.That(party.Get(ActorKind.Ylva).Min.Y + Heroes.HeroSize.Y, Is.EqualTo(8.2f).Within(.01f), "on Gorm");
        party.Jump(ActorKind.Ylva, 1);
        party.WalkTo(ActorKind.Ylva, 19.5f);
        party.Use(ActorKind.Ylva);
        party.WalkTo(ActorKind.Ylva, 26.5f);
        party.WalkTo(ActorKind.Gorm, 27.5f);
        party.WalkTo(ActorKind.Sigrid, 28.5f);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Won));
    }

    [Test]
    public void TheLongWayDown_EveryoneDoesTheirPart_AllReachTheExit()
    {
        var party = new Party(Levels.All[4].Rows);

        party.WalkTo(ActorKind.Gorm, 8f);
        party.Use(ActorKind.Gorm);
        party.WalkTo(ActorKind.Gorm, 10.3f);
        party.Wait(1.5f);
        Assert.That(party.Stage.IsChannelOn(0), Is.True, "crate fell onto the plate");

        party.Jump(ActorKind.Gorm, 1);
        party.WalkTo(ActorKind.Gorm, 22.5f);
        party.WalkTo(ActorKind.Ylva, 10.3f);
        party.Jump(ActorKind.Ylva, 1);
        party.WalkTo(ActorKind.Ylva, 25.3f);

        party.WalkTo(ActorKind.Sigrid, 11.5f, glide: true);
        party.WalkTo(ActorKind.Sigrid, 15.5f);
        party.Use(ActorKind.Sigrid);
        party.WalkTo(ActorKind.Sigrid, 23.5f);

        party.WalkTo(ActorKind.Ylva, 28.5f);
        party.WalkTo(ActorKind.Ylva, 22.5f);
        party.WalkTo(ActorKind.Gorm, 28.5f);
        party.WalkTo(ActorKind.Gorm, 24.5f);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Won));
    }

    [Test]
    public void TheLongWayDown_YlvaWalksIntoTheHole_IsKnockedOut()
    {
        var party = new Party(Levels.All[4].Rows) { FailOnKnockout = false };
        party.WalkTo(ActorKind.Gorm, 8f);
        party.Use(ActorKind.Gorm);
        party.WalkTo(ActorKind.Gorm, 10.3f);
        party.Wait(1.5f);

        party.WalkTo(ActorKind.Ylva, 11.5f);
        party.Wait(2f);

        Assert.That(party.Result, Is.EqualTo(StepResult.Failed(ActorKind.Ylva, FailCause.Fell)));
    }
}
