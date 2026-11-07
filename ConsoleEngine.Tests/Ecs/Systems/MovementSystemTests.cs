using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs.Systems;

public class MovementSystemTests
{
    [Test]
    public void Update_PositionAndVelocity_MovesByVelocityTimesDelta()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        group.Add(new MovementSystem());
        var mover = world.Create(new Position(new Vector2(1, 1)), new Velocity(new Vector2(4, -2)));
        var still = world.Create(new Position(new Vector2(5, 5)));

        group.Update(0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(mover).Value, Is.EqualTo(new Vector2(3, 0)));
            Assert.That(world.Get<Position>(still).Value, Is.EqualTo(new Vector2(5, 5)));
        });
    }

    [Test]
    public void Update_Filter_SkipsExcludedEntities()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        group.Add(new MovementSystem(Query.Without<Frozen>()));
        var frozen = world.Create(new Position(Vector2.Zero), new Velocity(Vector2.One), new Frozen());
        var mover = world.Create(new Position(Vector2.Zero), new Velocity(Vector2.One));

        group.Update(1f);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(frozen).Value, Is.EqualTo(Vector2.Zero));
            Assert.That(world.Get<Position>(mover).Value, Is.EqualTo(Vector2.One));
        });
    }
}
