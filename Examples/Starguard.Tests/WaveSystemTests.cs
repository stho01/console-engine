using NUnit.Framework;
using Starguard.Components;
using Starguard.Systems;

namespace Starguard.Tests;

public class WaveSystemTests
{
    [Test]
    public void Update_NoAliensLeft_StartsNextWave()
    {
        using var battle = new Battle();
        battle.Group.Add(new WaveSystem(battle.Match, battle.Formation, battle.Spawner));

        battle.Update(0.01f);

        Assert.Multiple(() =>
        {
            Assert.That(battle.Match.Wave, Is.EqualTo(2));
            Assert.That(battle.Count<Alien>(), Is.EqualTo(Spawner.Rows * Spawner.Columns));
            Assert.That(battle.Formation.StartCount, Is.EqualTo(Spawner.Rows * Spawner.Columns));
        });
    }

    [Test]
    public void Update_AliensLeft_KeepsWave()
    {
        using var battle = new Battle();
        battle.CreateAlien(10, 10);
        battle.Group.Add(new WaveSystem(battle.Match, battle.Formation, battle.Spawner));

        battle.Update(0.01f);

        Assert.That(battle.Match.Wave, Is.EqualTo(1));
    }
}
