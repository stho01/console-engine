using ConsoleEngine.Ecs.Systems;
using NUnit.Framework;
using Starguard.Systems;

namespace Starguard.Tests;

public class BoundsSystemTests
{
    [Test]
    public void Update_ShotsLeaveThePlayfield_AreDestroyed()
    {
        using var battle = new Battle();
        battle.Group.Add(new MovementSystem());
        battle.Group.Add(new BoundsSystem(battle.Playfield));
        var playerShot = battle.CreatePlayerShot(10, battle.Playfield.Top);
        var alienShot = battle.CreateAlienShot(10, battle.Playfield.Ground - 0.5f);
        var inFlight = battle.CreatePlayerShot(20, 30);

        battle.Update(0.1f);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.IsAlive(playerShot), Is.False);
            Assert.That(battle.World.IsAlive(alienShot), Is.False);
            Assert.That(battle.World.IsAlive(inFlight), Is.True);
        });
    }
}
