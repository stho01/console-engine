using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs.Systems;

/// <summary>Draws <see cref="Grass"/> as a dark green <c>√</c>, but only on cells nothing was drawn on yet.</summary>
public sealed class GrassRenderSystem(RenderConsole console, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const char Blade = '√';

    private Query _grass;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _grass = World.Query(Query.With<Grass, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var cameraPosition = camera.Position;
        foreach (var chunk in _grass.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var screen = positions[i].Value - cameraPosition;
                var x = (int)screen.X;
                var y = (int)screen.Y;
                if (console.GetCharAt(x, y) == ' ')
                    console.Draw(x, y, Blade, ConsoleColor.DarkGreen, ConsoleColor.Black);
            }
        }
    }
}
