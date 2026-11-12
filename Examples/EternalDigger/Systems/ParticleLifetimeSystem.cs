using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Effects;

namespace EternalDigger.Systems;

/// <summary>Ages particles, updates their <c>Glyph</c> from <see cref="ParticleLooks"/> and destroys expired ones.</summary>
public sealed class ParticleLifetimeSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _particles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Particle, Glyph>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _particles.GetChunks())
        {
            var entities = chunk.Entities;
            var particles = chunk.GetSpan<Particle>();
            var glyphs = chunk.GetSpan<Glyph>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var particle = ref particles[i];
                particle.Age += deltaSeconds;
                if (particle.Age >= particle.Lifetime)
                {
                    Commands.Destroy(entities[i]);
                    continue;
                }

                var progress = particle.Progress;
                glyphs[i].Character = ParticleLooks.Character(particle.Style, particle.Variant, progress);
                glyphs[i].Foreground = ParticleLooks.Color(particle.Style, particle.Variant, progress);
            }
        }
    }
}
