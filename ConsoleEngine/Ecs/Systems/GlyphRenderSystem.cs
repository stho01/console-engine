using System;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Ecs.Systems;

/// <summary>
/// Draws each <see cref="Glyph"/> at its <see cref="Position"/> relative to the camera. Off-screen cells are clipped.
/// </summary>
public sealed class GlyphRenderSystem(RenderConsole console, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _glyphs;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _glyphs = World.Query(Query.With<Position, Glyph>());

    protected override void OnUpdate(float deltaSeconds)
    {
        // Writes straight into the frame buffer: one bounds check per glyph, no call per cell.
        var pixels = console.Pixels;
        var width = console.Width;
        var height = console.Height;
        var cameraPosition = camera.Position;
        foreach (var chunk in _glyphs.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var glyphs = chunk.GetSpan<Glyph>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var screen = positions[i].Value - cameraPosition;
                var x = (int)MathF.Floor(screen.X);
                var y = (int)MathF.Floor(screen.Y);
                if ((uint)x >= (uint)width || (uint)y >= (uint)height)
                    continue;

                ref var cell = ref pixels[y * width + x];
                cell.Char = glyphs[i].Character;
                cell.ForegroundColor = glyphs[i].Foreground;
                cell.BackgroundColor = glyphs[i].Background;
            }
        }
    }
}
