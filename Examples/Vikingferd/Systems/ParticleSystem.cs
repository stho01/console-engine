using ConsoleEngine.Ecs;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Systems;

/// <summary>
/// Moves particles, stops them on solid tiles (debris lands on the floor instead of falling through it) and destroys
/// them when their life runs out.
/// </summary>
public sealed class ParticleSystem(Stage stage) : EntitySystem
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
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var particle = ref particles[i];
                particle.Life -= deltaSeconds;
                if (particle.Life <= 0f)
                {
                    Commands.Destroy(entities[i]);
                    continue;
                }

                particle.Velocity.Y += particle.Gravity * deltaSeconds;
                var next = particle.Position + particle.Velocity * deltaSeconds;
                if (stage.IsSolid((int)System.MathF.Floor(next.X), (int)System.MathF.Floor(next.Y)))
                {
                    particle.Velocity *= 0f;
                    continue;
                }

                particle.Position = next;
            }
        }
    }
}
