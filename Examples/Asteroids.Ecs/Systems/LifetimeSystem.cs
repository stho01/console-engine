using Asteroids.Ecs.Components;
using ConsoleEngine.Ecs;

namespace Asteroids.Ecs.Systems;

/// <summary>Counts <see cref="Lifetime"/> down and destroys entities that run out.</summary>
public sealed class LifetimeSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _mortals;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _mortals = World.Query(Query.With<Lifetime>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _mortals.GetChunks())
        {
            var lifetimes = chunk.GetSpan<Lifetime>();
            for (var i = 0; i < chunk.Count; i++)
            {
                lifetimes[i].SecondsLeft -= deltaSeconds;
                if (lifetimes[i].SecondsLeft <= 0f)
                    Commands.Destroy(chunk.Entities[i]);
            }
        }
    }
}
