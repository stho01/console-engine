using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;

namespace EternalDigger.Systems;

/// <summary>Floats <see cref="FloatingText"/>s upwards, slowing down, and destroys them when their time is up.</summary>
public sealed class FloatingTextSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float Drag = 1.2f;

    private Query _texts;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _texts = World.Query(Query.With<FloatingText, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _texts.GetChunks())
        {
            var entities = chunk.Entities;
            var texts = chunk.GetSpan<FloatingText>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var text = ref texts[i];
                text.Age += deltaSeconds;
                if (text.Age >= text.Lifetime)
                {
                    Commands.Destroy(entities[i]);
                    continue;
                }

                velocities[i].Value *= Math.Max(0f, 1f - Drag * deltaSeconds);
                positions[i].Value += velocities[i].Value * deltaSeconds;
            }
        }
    }
}
