using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using Swarm.Components;

namespace Swarm.Systems;

/// <summary><c>SPACE</c> adds <see cref="Step"/> particles, <c>BACKSPACE</c> removes up to that many.</summary>
public sealed class SpawnControlSystem(ParticleSpawner spawner) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Step = 10_000;

    private Query _particles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Particle>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (Input.Instance.GetKey(Key.SPACE).Pressed)
            spawner.Create(Commands, Step);

        if (Input.Instance.GetKey(Key.BACKSPACE).Pressed)
            DestroySome(Step);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DestroySome(int count)
    {
        foreach (var chunk in _particles.GetChunks())
        {
            foreach (var particle in chunk.Entities)
            {
                if (count-- == 0)
                    return;

                Commands.Destroy(particle);
            }
        }
    }
}
