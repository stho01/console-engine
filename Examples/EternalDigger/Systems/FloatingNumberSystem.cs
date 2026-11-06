using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;

namespace EternalDigger.Systems;

/// <summary>Floats <see cref="FloatingNumber"/>s upwards, slowing down, and destroys them when their time is up.</summary>
public sealed class FloatingNumberSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float Drag = 1.2f;

    private Query _numbers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _numbers = World.Query(Query.With<FloatingNumber, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _numbers.GetChunks())
        {
            var entities = chunk.Entities;
            var numbers = chunk.GetSpan<FloatingNumber>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var number = ref numbers[i];
                number.Age += deltaSeconds;
                if (number.Age >= number.Lifetime)
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
