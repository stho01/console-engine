using System;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;
using Vikingferd.Components;
using Vikingferd.Rendering;

namespace Vikingferd.Systems;

/// <summary>Draws every particle as one pixel, in its fade colour for the last third of its life.</summary>
public sealed class ParticleRenderSystem(Canvas canvas, Point origin) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _particles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Particle>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _particles.GetChunks())
        {
            var particles = chunk.GetSpan<Particle>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var particle = ref particles[i];
                var color = particle.Life < particle.MaxLife / 3f ? particle.FadeColor : particle.Color;
                canvas.Pixel(origin.X + (int)MathF.Floor(particle.Position.X * Layout.TilePixels),
                    origin.Y + (int)MathF.Floor(particle.Position.Y * Layout.TilePixels), color);
            }
        }
    }
}
