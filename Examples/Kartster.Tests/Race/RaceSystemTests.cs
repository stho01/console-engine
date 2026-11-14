using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Race;
using NUnit.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Tests.Race;

public class RaceSystemTests
{
    private const float Step = 1f / 120f;

    // A straight: the line across x = 0 facing +x, a checkpoint at x = 100.
    private static RaceSession Session() => new(
        new RaceGate(new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 0), 5, 5),
        [new RaceGate(new System.Numerics.Vector2(100, 0), new System.Numerics.Vector2(1, 0), 5, 5)],
        lapCount: 3);

    private static (SystemGroup Group, Entity Kart) Race(World world, RaceSession session, float x)
    {
        var kart = world.Create(new Position(new Vector2(x, 0)), new Components.Kart(), new RaceProgress());
        session.Register(kart.Id);
        var group = new SystemGroup(world);
        group.Add(new RaceSystem(session));
        return (group, kart);
    }

    [Test]
    public void Update_KartDrivenThroughGates_MirrorsTheRacer()
    {
        using var world = new World();
        var session = Session();
        var (group, kart) = Race(world, session, -5);
        session.Advance(RaceSession.CountdownSeconds);

        for (var x = -5f; x < 105; x += 0.5f)
        {
            world.Get<Position>(kart).Value = new Vector2(x, 0);
            group.Update(Step);
        }

        ref readonly var progress = ref world.Get<RaceProgress>(kart);
        Assert.That(progress.LapStarted, Is.True);
        Assert.That(progress.NextCheckpointIndex, Is.EqualTo(1));
        Assert.That(progress.Lap, Is.EqualTo(1));
        Assert.That(progress.Position, Is.EqualTo(1));
        Assert.That(session.RaceTimeSeconds, Is.GreaterThan(0f), "the system advances the clock");
    }

    [Test]
    public void Update_AfterForget_TheJumpCrossesNothing()
    {
        using var world = new World();
        var session = Session();
        var (group, kart) = Race(world, session, -2);
        group.Update(Step);

        world.Get<Position>(kart).Value = new Vector2(2, 0);
        world.Get<RaceProgress>(kart).Forget();
        group.Update(Step);

        Assert.That(world.Get<RaceProgress>(kart).LapStarted, Is.False);
    }

    [Test]
    public void Update_UnregisteredKart_Throws()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        group.Add(new RaceSystem(Session()));
        world.Create(new Position(Vector2.Zero), new Components.Kart(), new RaceProgress());

        Assert.That(() => group.Update(Step), Throws.TypeOf<System.Collections.Generic.KeyNotFoundException>());
    }

    [Test]
    public void Update_InSteadyState_DoesNotAllocate()
    {
        using var world = new World();
        var session = Session();
        var (group, kart) = Race(world, session, -5);
        var x = -5f;
        for (var step = 0; step < 120; step++, x += 0.1f)
        {
            world.Get<Position>(kart).Value = new Vector2(x, 0);
            group.Update(Step);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 1000; step++, x += 0.1f)
        {
            world.Get<Position>(kart).Value = new Vector2(x, 0);
            group.Update(Step);
        }

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
