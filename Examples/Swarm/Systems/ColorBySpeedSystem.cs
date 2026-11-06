using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Swarm.Components;

namespace Swarm.Systems;

/// <summary>Colours each particle by its speed: dark blue when slow, through blue and cyan, to white when fast.</summary>
public sealed class ColorBySpeedSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _particles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Particle, Velocity, Glyph>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var job = new ColorJob();
        _particles.ForEach<ColorJob, Velocity, Glyph>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct ColorJob : IForEach<Velocity, Glyph>
    {
        // Compared squared, so no square root per particle. Thresholds 15, 30 and 50 cells/s.
        public void Execute(ref Velocity velocity, ref Glyph glyph) =>
            glyph.Foreground = velocity.Value.LengthSquared() switch
            {
                < 15f * 15f => ConsoleColor.DarkBlue,
                < 30f * 30f => ConsoleColor.Blue,
                < 50f * 50f => ConsoleColor.Cyan,
                _ => ConsoleColor.White
            };
    }
}
