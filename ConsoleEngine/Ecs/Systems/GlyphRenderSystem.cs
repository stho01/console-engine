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
        var cameraPosition = camera.Position;
        foreach (var chunk in _glyphs.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var glyphs = chunk.GetSpan<Glyph>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var screen = positions[i].Value - cameraPosition;
                console.Draw((int)MathF.Floor(screen.X), (int)MathF.Floor(screen.Y),
                    glyphs[i].Character, glyphs[i].Foreground, glyphs[i].Background);
            }
        }
    }
}
