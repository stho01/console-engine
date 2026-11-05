using System;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Ecs.Systems;

/// <summary>
/// Draws each <see cref="SpriteRef"/> with its top-left cell at <c>floor(Position - Camera.Position) + Offset</c>.
/// Sprites entirely off-screen are skipped. <paramref name="filter"/> narrows the entities, e.g.
/// <c>Query.With&lt;Tile&gt;()</c>: a query visits archetypes in no defined order, so use one
/// instance per layer, added in drawing order, to control what is drawn on top.
/// </summary>
public sealed class SpriteRenderSystem(
    RenderConsole console, Camera camera, SpriteLibrary sprites, QueryDescription filter = default) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _sprites;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _sprites = World.Query(filter.With<Position, SpriteRef>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var cameraPosition = camera.Position;
        foreach (var chunk in _sprites.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var refs = chunk.GetSpan<SpriteRef>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var sprite = sprites.Get(refs[i].Id);
                var screen = positions[i].Value - cameraPosition;
                var x = (int)MathF.Floor(screen.X) + refs[i].Offset.X;
                var y = (int)MathF.Floor(screen.Y) + refs[i].Offset.Y;

                if (x + sprite.Width <= 0 || x >= console.Width || y + sprite.Height <= 0 || y >= console.Height)
                    continue;

                console.Draw(x, y, sprite);
            }
        }
    }
}
