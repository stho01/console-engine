using System;
using ConsoleEngine.Abstractions.Rendering;
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
        var pixels = console.Pixels;
        var width = console.Width;
        var height = console.Height;
        var cameraPosition = camera.Position;
        foreach (var chunk in _sprites.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var refs = chunk.GetSpan<SpriteRef>();
            var offsets = chunk.Has<RenderOffset>() ? chunk.GetSpan<RenderOffset>() : Span<RenderOffset>.Empty;
            for (var i = 0; i < chunk.Count; i++)
            {
                var sprite = sprites.Get(refs[i].Id);
                var screen = positions[i].Value - cameraPosition;
                if (!offsets.IsEmpty)
                    screen += offsets[i].Position;
                var x = (int)MathF.Floor(screen.X) + refs[i].Offset.X;
                var y = (int)MathF.Floor(screen.Y) + refs[i].Offset.Y;
                CopyClipped(sprite.Data, x, y, pixels, width, height, console);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// Copies the part of the sprite that is on screen straight into the frame buffer. The visible rectangle is worked
    /// out once, so the inner loop has no bounds checks of its own. Sprite data is indexed <c>[x, y]</c>.
    /// </summary>
    private static void CopyClipped(Pixel[,] data, int x, int y, Span<Pixel> pixels, int width, int height,
        RenderConsole console)
    {
        var fromX = Math.Max(0, -x);
        var toX = Math.Min(data.GetLength(0), width - x);
        var fromY = Math.Max(0, -y);
        var toY = Math.Min(data.GetLength(1), height - y);

        for (var spriteY = fromY; spriteY < toY; spriteY++)
        {
            // x can be negative (sprite partly left of the screen); rowStart + spriteX never is.
            var rowStart = (y + spriteY) * width + x;
            for (var spriteX = fromX; spriteX < toX; spriteX++)
                pixels[rowStart + spriteX] = data[spriteX, spriteY];
            if (toX > fromX)
                console.UsePalette(rowStart + fromX, toX - fromX);
        }
    }
}
