using ConsoleEngine.Ecs.Components;
using NUnit.Framework;
using Starguard.Assets;
using Starguard.Components;
using Starguard.Systems;

namespace Starguard.Tests;

public class CannonSystemTests
{
    [Test]
    public void Update_MoveRight_MovesBySpeedTimesDelta()
    {
        using var battle = CreateBattle();
        var cannon = battle.CreateCannon(40);
        battle.Controls.Move = 1;

        battle.Update(0.1f);

        Assert.That(battle.World.Get<Position>(cannon).Value.X, Is.EqualTo(40 + Spawner.CannonSpeed * 0.1f));
    }

    [Test]
    public void Update_MoveRightAtEdge_StaysOnScreen()
    {
        using var battle = CreateBattle();
        var maxX = battle.Playfield.Width - SpriteSheet.UnitWidth;
        var cannon = battle.CreateCannon(maxX);
        battle.Controls.Move = 1;

        battle.Update(0.1f);

        Assert.That(battle.World.Get<Position>(cannon).Value.X, Is.EqualTo(maxX));
    }

    [Test]
    public void Update_FireHeldWhileShotInFlight_FiresOnlyOnce()
    {
        using var battle = CreateBattle();
        battle.CreateCannon(40);
        battle.Controls.Fire = true;

        battle.Update(0.01f);
        battle.Update(0.01f);

        Assert.That(battle.Count<PlayerShot>(), Is.EqualTo(1));
    }

    [Test]
    public void Update_CannonDown_NeitherMovesNorFires()
    {
        using var battle = CreateBattle();
        var cannon = battle.CreateCannon(40);
        battle.Match.HitCannon();
        battle.Controls.Move = 1;
        battle.Controls.Fire = true;

        battle.Update(0.1f);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.Get<Position>(cannon).Value.X, Is.EqualTo(40));
            Assert.That(battle.Count<PlayerShot>(), Is.Zero);
        });
    }

    [Test]
    public void Update_RespawnTimeOver_RebuildsCannonAtStart()
    {
        using var battle = CreateBattle();
        var cannon = battle.CreateCannon(3);
        battle.Match.HitCannon();
        battle.World.Get<SpriteRef>(cannon).Id = battle.Sprites.Wreck;

        battle.Update(Match.RespawnSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(battle.Match.CannonDown, Is.False);
            Assert.That(battle.World.Get<Position>(cannon).Value, Is.EqualTo(battle.Spawner.CannonStart));
            Assert.That(battle.World.Get<SpriteRef>(cannon).Id, Is.EqualTo(battle.Sprites.Cannon));
        });
    }

    private static Battle CreateBattle()
    {
        var battle = new Battle();
        battle.Group.Add(new CannonSystem(battle.Match, battle.Controls, battle.Spawner, battle.Playfield));
        return battle;
    }
}
