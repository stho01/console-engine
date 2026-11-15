using ConsoleEngine.Ecs.Components;
using NUnit.Framework;
using Starguard.Components;
using Starguard.Systems;

namespace Starguard.Tests;

public class CollisionSystemTests
{
    // At this delta a player shot covers 5 rows, an alien shot almost 2.
    private const float Delta = 0.1f;

    [Test]
    public void Update_PlayerShotPassedThroughAlien_DestroysBothAndScores()
    {
        using var battle = CreateBattle();
        var alien = battle.CreateAlien(10, 10, points: 30);
        var shot = battle.CreatePlayerShot(12, 8);

        battle.Update(Delta);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.IsAlive(alien), Is.False);
            Assert.That(battle.World.IsAlive(shot), Is.False);
            Assert.That(battle.Match.Score, Is.EqualTo(30));
            Assert.That(battle.Count<Lifetime>(), Is.EqualTo(1), "explosion");
        });
    }

    [Test]
    public void Update_PlayerShotBesideAlien_MissesIt()
    {
        using var battle = CreateBattle();
        var alien = battle.CreateAlien(10, 10);
        var shot = battle.CreatePlayerShot(15, 8);

        battle.Update(Delta);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.IsAlive(alien), Is.True);
            Assert.That(battle.World.IsAlive(shot), Is.True);
        });
    }

    [Test]
    public void Update_PlayerShotThroughShield_ChipsNearestCellAndStops()
    {
        using var battle = CreateBattle();
        var lower = battle.CreateShieldCell(12, 41);
        var upper = battle.CreateShieldCell(12, 40);
        var alien = battle.CreateAlien(10, 36);
        var shot = battle.CreatePlayerShot(12, 38);

        battle.Update(Delta);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.Get<ShieldCell>(lower).Health, Is.EqualTo(Spawner.ShieldHealth - 1));
            Assert.That(battle.World.Get<Glyph>(lower).Character, Is.EqualTo('▓'));
            Assert.That(battle.World.Get<ShieldCell>(upper).Health, Is.EqualTo(Spawner.ShieldHealth));
            Assert.That(battle.World.IsAlive(alien), Is.True);
            Assert.That(battle.World.IsAlive(shot), Is.False);
        });
    }

    [Test]
    public void Update_LastHealthChippedOff_DestroysShieldCell()
    {
        using var battle = CreateBattle();
        var cell = battle.CreateShieldCell(12, 41, health: 1);
        battle.CreateAlienShot(12, 41);

        battle.Update(Delta);

        Assert.That(battle.World.IsAlive(cell), Is.False);
    }

    [Test]
    public void Update_AlienShotHitsCannon_WrecksItAndCostsALife()
    {
        using var battle = CreateBattle();
        var cannon = battle.CreateCannon(20);
        battle.CreateAlienShot(22, battle.Playfield.CannonTop);

        battle.Update(Delta);

        Assert.Multiple(() =>
        {
            Assert.That(battle.Match.Lives, Is.EqualTo(Match.StartingLives - 1));
            Assert.That(battle.Match.CannonDown, Is.True);
            Assert.That(battle.World.Get<SpriteRef>(cannon).Id, Is.EqualTo(battle.Sprites.Wreck));
        });
    }

    [Test]
    public void Update_AlienShotHitsCannonOnLastLife_EndsMatch()
    {
        using var battle = CreateBattle();
        battle.CreateCannon(20);
        for (var i = 1; i < Match.StartingLives; i++)
        {
            battle.Match.HitCannon();
            battle.Match.RespawnSecondsLeft = 0f;
        }

        battle.CreateAlienShot(22, battle.Playfield.CannonTop);

        battle.Update(Delta);

        Assert.That(battle.Match.IsOver, Is.True);
    }

    [Test]
    public void Update_TwoAlienShotsHitCannonInOneFrame_CostsOneLife()
    {
        using var battle = CreateBattle();
        battle.CreateCannon(20);
        battle.CreateAlienShot(21, battle.Playfield.CannonTop);
        battle.CreateAlienShot(23, battle.Playfield.CannonTop);

        battle.Update(Delta);

        Assert.That(battle.Match.Lives, Is.EqualTo(Match.StartingLives - 1));
    }

    [Test]
    public void Update_AlienOverShield_ErasesCoveredCells()
    {
        using var battle = CreateBattle();
        var covered = battle.CreateShieldCell(12, 41);
        var free = battle.CreateShieldCell(12, 43);
        battle.CreateAlien(10, 40);

        battle.Update(Delta);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.IsAlive(covered), Is.False);
            Assert.That(battle.World.IsAlive(free), Is.True);
        });
    }

    private static Battle CreateBattle()
    {
        var battle = new Battle();
        battle.Group.Add(new CollisionSystem(battle.Match, battle.Spawner));
        return battle;
    }
}
