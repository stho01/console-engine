using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using Starguard.Systems;

namespace Starguard.Tests;

public class FormationSystemTests
{
    private const float AlienY = 10;

    [Test]
    public void StepSeconds_FullWave_ReturnsSlowest()
    {
        var formation = new Formation();
        formation.Reset(55);

        Assert.That(formation.StepSeconds(55), Is.EqualTo(Formation.SlowestStepSeconds));
    }

    [Test]
    public void StepSeconds_OneAlienLeft_ReturnsFastest()
    {
        var formation = new Formation();
        formation.Reset(55);

        Assert.That(formation.StepSeconds(1), Is.EqualTo(Formation.FastestStepSeconds));
    }

    [Test]
    public void Update_BeforeStepInterval_DoesNotMove()
    {
        using var battle = CreateBattle(40, out var alien);

        battle.Update(Formation.FastestStepSeconds / 2);

        Assert.That(battle.World.Get<Position>(alien).Value, Is.EqualTo(new Vector2(40, AlienY)));
    }

    [Test]
    public void Update_AfterStepInterval_StepsSidewaysAndFlipsFrame()
    {
        using var battle = CreateBattle(40, out var alien);

        battle.Update(Formation.FastestStepSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.Get<Position>(alien).Value,
                Is.EqualTo(new Vector2(40 + Formation.StepCells, AlienY)));
            Assert.That(battle.World.Get<SpriteRef>(alien).Id, Is.EqualTo(battle.Sprites.Brute + 1));
        });
    }

    [Test]
    public void Update_StepWouldCrossRightEdge_DropsAndReverses()
    {
        var x = Playfield.Default.Width - 6;
        using var battle = CreateBattle(x, out var alien);

        battle.Update(Formation.FastestStepSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(battle.World.Get<Position>(alien).Value,
                Is.EqualTo(new Vector2(x, AlienY + Formation.DropCells)));
            Assert.That(battle.Formation.Direction, Is.EqualTo(-1));
        });
    }

    [Test]
    public void Update_AlienDropsIntoCannonRow_EndsMatch()
    {
        using var battle = CreateBattle(0, out var alien);
        battle.Formation.Direction = -1;
        battle.World.Get<Position>(alien).Value.Y = battle.Playfield.CannonTop - 2;

        battle.Update(Formation.FastestStepSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(battle.Match.IsOver, Is.True);
            Assert.That(battle.Match.Lives, Is.Zero);
        });
    }

    /// <summary>A formation of one alien, which steps at <see cref="Formation.FastestStepSeconds"/>.</summary>
    private static Battle CreateBattle(float x, out Entity alien)
    {
        var battle = new Battle();
        alien = battle.CreateAlien(x, AlienY);
        battle.Formation.Reset(1);
        battle.Group.Add(new FormationSystem(battle.Match, battle.Formation, battle.Playfield));
        return battle;
    }
}
