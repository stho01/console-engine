using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Physics.Tests;

public class InterpolationTests
{
    private const float Step = 1f / 64f;

    [Test]
    public void Update_HalfwayToNextStep_DrawsHalfAStepBehind()
    {
        using var world = new World();
        var timestep = new FixedTimestep(Step);
        var fixedGroup = new SystemGroup(world);
        fixedGroup.Add(new PhysicsSystem());
        var presentation = new SystemGroup(world);
        presentation.Add(new InterpolationSystem(timestep));
        var body = world.Create(new Position(Vector2.Zero), new Velocity(new Vector2(64, 0)), RigidBody.Kinematic(),
            new Interpolation(), new RenderOffset());

        // One step moves 1 cell; then half a step of time passes without a step.
        timestep.Advance(Step);
        fixedGroup.Update(Step);
        timestep.Advance(Step * .5f);
        presentation.Update(0f);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(body).Value.X, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(world.Get<RenderOffset>(body).Position.X, Is.EqualTo(-.5f).Within(1e-4f),
                "drawn at 0.5: between the last two steps");
        });
    }

    [Test]
    public void Update_BeforeFirstStep_NoOffset()
    {
        using var world = new World();
        var presentation = new SystemGroup(world);
        presentation.Add(new InterpolationSystem(new FixedTimestep(Step)));
        var body = world.Create(new Position(new Vector2(5, 5)), new Interpolation(), new RenderOffset());

        presentation.Update(0f);

        Assert.That(world.Get<RenderOffset>(body).Position, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void Update_TurningBody_InterpolatesRotation()
    {
        using var world = new World();
        var timestep = new FixedTimestep(Step);
        var fixedGroup = new SystemGroup(world);
        fixedGroup.Add(new PhysicsSystem());
        var presentation = new SystemGroup(world);
        presentation.Add(new InterpolationSystem(timestep));
        var body = world.Create(new Position(Vector2.Zero), new Velocity(Vector2.Zero), new Rotation(0f),
            new AngularVelocity(64f), RigidBody.Kinematic(), new Interpolation());
        world.Add(body, new RenderOffset());

        timestep.Advance(Step);
        fixedGroup.Update(Step);
        presentation.Update(0f);

        // Right after a step alpha is 0: drawn a whole step behind, at the previous rotation.
        Assert.That(world.Get<RenderOffset>(body).Radians, Is.EqualTo(-1f).Within(1e-3f));
    }
}
