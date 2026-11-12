using NUnit.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Tests;

/// <summary>Each hero ability and each hazard on a small map, through <see cref="Party"/>.</summary>
public class PuzzleRulesTests
{
    /// <summary>A room with a three-tile wall at column 8; the floor is at y = 9.</summary>
    private static readonly string[] HighWall =
    [
        "############",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "#.......#..#",
        "#.......#..#",
        "#YGS....#..#",
        "############"
    ];

    [Test]
    public void Jump_Ylva_ClearsAThreeTileWall()
    {
        var party = new Party(HighWall);

        party.WalkTo(ActorKind.Ylva, 7.6f);
        party.Jump(ActorKind.Ylva, 1);
        party.WalkTo(ActorKind.Ylva, 10f);

        Assert.That(Party.CenterX(party.Get(ActorKind.Ylva)), Is.GreaterThan(9f));
    }

    [TestCase(ActorKind.Gorm)]
    [TestCase(ActorKind.Sigrid)]
    public void Jump_GormOrSigrid_CannotClearAThreeTileWall(ActorKind hero)
    {
        var party = new Party(HighWall);

        party.WalkTo(hero, 7.6f);
        party.Jump(hero, 1);
        party.WalkTo(hero, 10f);

        Assert.That(Party.CenterX(party.Get(hero)), Is.LessThan(8f));
    }

    [Test]
    public void Jump_YlvaOnGormsHead_LandsOnHim()
    {
        var party = new Party(HighWall);

        party.WalkTo(ActorKind.Gorm, 5.5f);
        party.WalkTo(ActorKind.Ylva, 5.5f);
        party.Jump(ActorKind.Ylva);

        ref var gorm = ref party.Get(ActorKind.Gorm);
        ref var ylva = ref party.Get(ActorKind.Ylva);
        Assert.That(ylva.Min.Y + ylva.Size.Y, Is.EqualTo(gorm.Min.Y).Within(.01f));
        Assert.That(ylva.OnGround, Is.True);
    }

    [Test]
    public void Walk_HeroesPassThroughEachOther()
    {
        var party = new Party(HighWall);

        party.WalkTo(ActorKind.Ylva, 6f);

        Assert.That(Party.CenterX(party.Get(ActorKind.Ylva)), Is.EqualTo(6f).Within(.1f));
    }

    private static readonly string[] CrateRoom =
    [
        "##########",
        "#........#",
        "#........#",
        "#G.S.o...#",
        "##########"
    ];

    [Test]
    public void Push_Gorm_MovesTheCrate()
    {
        var party = new Party(CrateRoom);

        party.WalkTo(ActorKind.Gorm, 8.5f);

        Assert.That(party.Crate().Min.X, Is.EqualTo(8f).Within(.01f), "pushed to the wall");
    }

    [Test]
    public void Push_Sigrid_CannotMoveTheCrate()
    {
        var party = new Party(CrateRoom);

        party.WalkTo(ActorKind.Sigrid, 7f);

        Assert.That(party.Crate().Min.X, Is.EqualTo(5f));
        Assert.That(party.Get(ActorKind.Sigrid).Min.X + Heroes.HeroSize.X, Is.EqualTo(5f).Within(.01f));
    }

    [Test]
    public void Push_CrateOverAOneTileHole_DropsIn()
    {
        var party = new Party(
        [
            "##########",
            "#........#",
            "#G.o.....#",
            "#####.####",
            "##########"
        ]);

        party.WalkTo(ActorKind.Gorm, 8f);

        Assert.That(party.Crate().Min, Is.EqualTo(new Microsoft.Xna.Framework.Vector2(5f, 3f)));
        Assert.That(Party.CenterX(party.Get(ActorKind.Gorm)), Is.GreaterThan(7f), "walked over the filled hole");
    }

    private static readonly string[] CrackedWall =
    [
        "########",
        "#..#...#",
        "#G.%...#",
        "#S.%...#",
        "########"
    ];

    [Test]
    public void Use_GormFacingCrackedStone_SmashesIt()
    {
        var party = new Party(CrackedWall);

        party.WalkTo(ActorKind.Gorm, 3f);
        party.Use(ActorKind.Gorm);

        Assert.That(party.Stage.KindAt(3, 2), Is.EqualTo(TileKind.Air));
        Assert.That(party.Stage.KindAt(3, 3), Is.EqualTo(TileKind.Air));
        Assert.That(party.Stage.KindAt(3, 1), Is.EqualTo(TileKind.Rock));
    }

    [Test]
    public void Use_GormFacingAway_SmashesNothing()
    {
        var party = new Party(CrackedWall);

        party.WalkTo(ActorKind.Gorm, 3f);
        party.Face(ActorKind.Gorm, -1);
        party.Use(ActorKind.Gorm);

        Assert.That(party.Stage.KindAt(3, 3), Is.EqualTo(TileKind.Cracked));
    }

    [Test]
    public void Use_SigridAtCrackedStone_SmashesNothing()
    {
        var party = new Party(CrackedWall);

        party.WalkTo(ActorKind.Sigrid, 3f);
        party.Use(ActorKind.Sigrid);

        Assert.That(party.Stage.KindAt(3, 3), Is.EqualTo(TileKind.Cracked));
    }

    /// <summary>A five-tile drop from the ledge on the left.</summary>
    private static readonly string[] Cliff =
    [
        "##########",
        "#........#",
        "#GS......#",
        "###......#",
        "###......#",
        "###......#",
        "###......#",
        "###......#",
        "##########"
    ];

    [Test]
    public void Fall_FiveTilesWithoutGliding_KnocksOut()
    {
        var party = new Party(Cliff) { FailOnKnockout = false };

        party.WalkTo(ActorKind.Gorm, 5f);

        Assert.That(party.Result, Is.EqualTo(StepResult.Failed(ActorKind.Gorm, FailCause.Fell)));
    }

    [Test]
    public void Fall_SigridGliding_LandsSafely()
    {
        var party = new Party(Cliff);

        party.WalkTo(ActorKind.Sigrid, 5f, glide: true);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Playing));
        Assert.That(party.Get(ActorKind.Sigrid).Min.Y + Heroes.HeroSize.Y, Is.EqualTo(8f).Within(.01f));
    }

    [Test]
    public void Fall_SigridNotGliding_KnocksOut()
    {
        var party = new Party(Cliff) { FailOnKnockout = false };

        party.WalkTo(ActorKind.Sigrid, 5f);

        Assert.That(party.Result, Is.EqualTo(StepResult.Failed(ActorKind.Sigrid, FailCause.Fell)));
    }

    [Test]
    public void Fall_FourTiles_IsSafe()
    {
        var party = new Party(
        [
            "########",
            "#G.....#",
            "##.....#",
            "##.....#",
            "##.....#",
            "##.....#",
            "########"
        ]);

        party.WalkTo(ActorKind.Gorm, 4f);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Playing));
    }

    [Test]
    public void Spikes_HeroWalksIn_KnocksOut()
    {
        var party = new Party(
        [
            "########",
            "#......#",
            "#Y..^..#",
            "########"
        ]) { FailOnKnockout = false };

        party.WalkTo(ActorKind.Ylva, 6f);

        Assert.That(party.Result, Is.EqualTo(StepResult.Failed(ActorKind.Ylva, FailCause.Spikes)));
    }

    [Test]
    public void Exit_OnlyTwoHeroesThere_NotWon()
    {
        var party = new Party(
        [
            "########",
            "#......#",
            "#YGS.XX#",
            "########"
        ]);

        party.WalkTo(ActorKind.Ylva, 5.5f);
        party.WalkTo(ActorKind.Gorm, 6.5f);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Playing));

        party.WalkTo(ActorKind.Sigrid, 6f);

        Assert.That(party.Result.Outcome, Is.EqualTo(Outcome.Won));
    }

    [Test]
    public void Jump_RequestedInTheAir_IsDropped()
    {
        var party = new Party(Cliff);
        ref var gorm = ref party.Get(ActorKind.Gorm);
        gorm.OnGround = false;
        gorm.JumpRequested = true;

        party.Step();

        Assert.That(party.Get(ActorKind.Gorm).JumpRequested, Is.False);
        Assert.That(party.Get(ActorKind.Gorm).Velocity.Y, Is.GreaterThanOrEqualTo(0f));
    }
}
