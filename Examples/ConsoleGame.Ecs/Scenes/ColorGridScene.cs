using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using ConsoleGame.Ecs.Systems;
using Microsoft.Xna.Framework;

namespace ConsoleGame.Ecs.Scenes;

/// <summary>One entity per cell. They start black (invisible, like the original before its first interval).</summary>
public sealed class ColorGridScene : EcsScene<ConsoleEcsGame>
{
    protected override void OnCreate()
    {
        for (var y = 0; y < Console.Height; y++)
        for (var x = 0; x < Console.Width; x++)
            World.Create(new Position(new Vector2(x, y)), new Glyph('█', ConsoleColor.Black));

        Simulation.Add(new RecolorSystem());
        Presentation.Add(new GlyphRenderSystem(Console, Camera));
    }
}
