using System;
using Asteroids.Ecs.Systems;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;

namespace Asteroids.Ecs.Scenes;

/// <summary>
/// The whole game: entities from <see cref="Spawner"/>, game-specific systems from <c>Systems/</c>, and the
/// engine's <see cref="PhysicsSystem"/>, <see cref="MovementSystem"/> and render systems. The order of <c>Add</c> calls
/// is the order they run. Asteroids and bullets are physics bodies and move in the fixed step; the ship has no
/// <c>RigidBody</c> and moves every frame with <see cref="MovementSystem"/>.
/// </summary>
public sealed class AsteroidsScene(int asteroidCount) : EcsScene<AsteroidsEcsGame>
{
    protected override void OnCreate()
    {
        // Same spawn area as Examples/Asteroids: three screens wide and high, centred on the start screen.
        var playfield = new Playfield(
            new Vector2(-Console.Width, -Console.Height), new Vector2(Console.Width * 3, Console.Height * 3));
        var outlines = new OutlineLibrary();
        var random = new Random();
        var spawner = new Spawner(World, outlines, random);

        spawner.CreateShip(Console.ScreenCenter);
        for (var i = 0; i < asteroidCount; i++)
            spawner.CreateAsteroid(playfield.Min + new Vector2(
                (float)random.NextDouble() * playfield.Size.X, (float)random.NextDouble() * playfield.Size.Y));

        // About the size of an asteroid (radius 8 to 11).
        var physics = FixedSimulation.Add(new PhysicsSystem(cellSize: 16f));
        FixedSimulation.Add(new CollisionSystem(physics));

        Simulation.Add(new ShipControlSystem());
        Simulation.Add(new MovementSystem(Query.Without<RigidBody>()));
        Simulation.Add(new WrapSystem(playfield));
        Simulation.Add(new LifetimeSystem());
        Simulation.Add(new CameraSystem(Camera, Console));

        Presentation.Add(new OutlineRenderSystem(Console, Camera, outlines));
        Presentation.Add(new GlyphRenderSystem(Console, Camera));
    }
}
