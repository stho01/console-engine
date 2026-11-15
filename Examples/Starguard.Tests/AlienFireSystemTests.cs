using System;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using Starguard.Assets;
using Starguard.Components;
using Starguard.Systems;

namespace Starguard.Tests;

public class AlienFireSystemTests
{
    [Test]
    public void Update_IntervalPassed_FiresFromLowestAlienInColumn()
    {
        using var battle = new Battle();
        battle.CreateAlien(10, 10);
        battle.CreateAlien(10, 13);
        battle.Group.Add(new AlienFireSystem(battle.Match, new Random(1)));

        battle.Update(AlienFireSystem.ShotInterval(1));

        var shots = battle.World.Query(ConsoleEngine.Ecs.Query.With<AlienShot, Position>());
        foreach (var chunk in shots.GetChunks())
        {
            var expected = new Vector2(10 + SpriteSheet.UnitWidth / 2, 13 + SpriteSheet.UnitHeight);
            Assert.That(chunk.GetSpan<Position>()[0].Value, Is.EqualTo(expected));
        }

        Assert.That(shots.Count, Is.EqualTo(1));
    }

    [Test]
    public void Update_MaxShotsFalling_HoldsFire()
    {
        using var battle = new Battle();
        battle.CreateAlien(10, 10);
        for (var i = 0; i < AlienFireSystem.MaxShots; i++)
            battle.CreateAlienShot(30 + i, 20);
        battle.Group.Add(new AlienFireSystem(battle.Match, new Random(1)));

        battle.Update(AlienFireSystem.ShotInterval(1));

        Assert.That(battle.Count<AlienShot>(), Is.EqualTo(AlienFireSystem.MaxShots));
    }

    [Test]
    public void ShotInterval_LateWave_NeverBelowMinimum() =>
        Assert.That(AlienFireSystem.ShotInterval(50), Is.EqualTo(0.35f));
}
