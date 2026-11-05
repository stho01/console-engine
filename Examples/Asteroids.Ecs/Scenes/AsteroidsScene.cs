using System;
using Asteroids.Ecs.Systems;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using Microsoft.Xna.Framework;

namespace Asteroids.Ecs.Scenes;

/// <summary>
/// The whole game: entities from <see cref="Spawner"/>, game-specific systems from <c>Systems/</c>, and the
/// engine's <see cref="MovementSystem"/> and render systems. The order of <c>Add</c> calls is the order they run.
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

        Simulation.Add(new ShipControlSystem());
        Simulation.Add(new MovementSystem());
        Simulation.Add(new WrapSystem(playfield));
        Simulation.Add(new CollisionSystem());
        Simulation.Add(new LifetimeSystem());
        Simulation.Add(new CameraSystem(Camera, Console));

        Presentation.Add(new OutlineRenderSystem(Console, Camera, outlines));
        Presentation.Add(new GlyphRenderSystem(Console, Camera));
    }
}
