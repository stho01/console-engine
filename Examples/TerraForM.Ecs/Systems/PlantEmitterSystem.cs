using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs.Systems;

/// <summary>
/// For 3 s after planting, every 150 ms each emitter spawns a ring of 30 <see cref="Grass"/> entities, each ring one
/// cell wider. Grass is never removed during the level.
/// </summary>
public sealed class PlantEmitterSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float LifetimeMs = 3000;
    private const float EmitIntervalMs = 150;
    private const int BladesPerRing = 30;

    private readonly Random _random = new();
    private Query _emitters;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _emitters = World.Query(Query.With<PlantEmitter, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var deltaMs = (float)GameTime.Delta.TotalMilliseconds;
        foreach (var chunk in _emitters.GetChunks())
        {
            var emitters = chunk.GetSpan<PlantEmitter>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var emitter = ref emitters[i];
                if (emitter.ElapsedMs >= LifetimeMs)
                    continue;

                emitter.ElapsedMs += deltaMs;
                emitter.SinceLastEmitMs += deltaMs;
                if (emitter.SinceLastEmitMs <= EmitIntervalMs)
                    continue;

                SpawnRing(ref emitter, positions[i].Value);
                emitter.SinceLastEmitMs = 0f;
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Same scatter as the original: blades at random angles, on a circle of radius rings + 1.</summary>
    private void SpawnRing(ref PlantEmitter emitter, Vector2 center)
    {
        emitter.Rings++;
        for (var i = 0; i < BladesPerRing; i++)
        {
            var angle = MathF.Tau / BladesPerRing * (float)(_random.NextDouble() - .25);
            var offset = new Vector2(MathF.Sin(angle * i), MathF.Cos(angle * i)) * (emitter.Rings + 1);
            Commands.Create(new Position(center + offset), new Grass());
        }
    }
}
