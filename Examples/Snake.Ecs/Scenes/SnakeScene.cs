using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using Microsoft.Xna.Framework;
using Snake.Ecs.Components;
using Snake.Ecs.Systems;

namespace Snake.Ecs.Scenes;

/// <summary>
/// A six-cell snake on the top row heading east: the head at (5, 0) and five segments behind it, (0, 0) the oldest.
/// </summary>
public sealed class SnakeScene : EcsScene<SnakeEcsGame>
{
    protected override void OnCreate()
    {
        World.Create(new Position(new Vector2(5, 0)), new Glyph('█', ConsoleColor.Red), new SnakeHead(Direction.East));
        for (var x = 0; x < 5; x++)
            World.Create(new Position(new Vector2(x, 0)), new Glyph('█', ConsoleColor.White), new SnakeSegment(x));

        Simulation.Add(new FoodSpawnSystem(Console.Width, Console.Height));
        Simulation.Add(new SteeringSystem());
        Simulation.Add(new SnakeMovementSystem());

        Presentation.Add(new GlyphRenderSystem(Console, Camera));
    }
}
