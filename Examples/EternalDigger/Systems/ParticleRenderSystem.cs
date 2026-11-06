using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// Draws particle glyphs over the world, keeping each cell's background. Dust and mist only show where light falls;
/// debris, sparks, coins and glints show everywhere (glints are how ore is spotted in the dark).
/// </summary>
public sealed class ParticleRenderSystem(Canvas canvas, Camera camera, LightMap light) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _particles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Particle, Position, Glyph>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var cameraPosition = camera.Position;
        foreach (var chunk in _particles.GetChunks())
        {
            var particles = chunk.GetSpan<Particle>();
            var positions = chunk.GetSpan<Position>();
            var glyphs = chunk.GetSpan<Glyph>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var world = positions[i].Value;
                if (NeedsLight(particles[i].Style) && IsDark(world.X, world.Y))
                    continue;

                var x = (int)MathF.Floor(world.X - cameraPosition.X);
                var y = (int)MathF.Floor(world.Y - cameraPosition.Y);
                canvas.DrawOver(x, y, glyphs[i].Character, glyphs[i].Foreground);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static bool NeedsLight(ParticleStyle style) => style is ParticleStyle.Dust or ParticleStyle.Mist;

    private bool IsDark(float worldX, float worldY) =>
        light.Get((int)MathF.Floor(worldX / Layout.TileWidth), (int)MathF.Floor(worldY / Layout.TileHeight)) ==
        LightMap.Dark;
}
