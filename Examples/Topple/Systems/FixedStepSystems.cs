using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using Topple.Components;
using Topple.Effects;

namespace Topple.Systems;

/// <summary>The fixed-step systems in run order, shared by the scene and the tests.</summary>
public static class FixedStep
{
    public static void AddSystems(SystemGroup group, PhysicsSystem physics, Wrecker wrecker, EffectSpawner effects)
    {
        group.Add(new FerrySystem());
        group.Add(new VelocityMemorySystem());
        group.Add(physics);
        group.Add(new ImpactDamageSystem(physics, wrecker));
        group.Add(new ShotSystem(physics, wrecker, effects));
        group.Add(new WaterSystem(physics, wrecker, effects));
    }
}

/// <summary>
/// Sets each ferry's velocity so that the physics moves it to where its sway puts it at the end of the step. A
/// kinematic body is moved only by its velocity, and contacts never push it.
/// </summary>
[UpdateBefore(typeof(PhysicsSystem))]
public sealed class FerrySystem : EntitySystem
{
    private Query _ferries;

    protected override void OnCreate() => _ferries = World.Query(Query.With<Ferry, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _ferries.GetChunks())
        {
            var ferries = chunk.GetSpan<Ferry>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var ferry = ref ferries[i];
                ferry.Time += deltaSeconds;
                var sway = ferry.Amplitude * MathF.Sin(ferry.Time * MathF.Tau / ferry.PeriodSeconds);
                var target = ferry.Anchor + new Vector2(sway, 0f);
                velocities[i].Value = (target - positions[i].Value) / deltaSeconds;
            }
        }
    }
}

/// <summary>Keeps each body's velocity from before the physics step, for <see cref="Impact.Jolt"/>.</summary>
[UpdateBefore(typeof(PhysicsSystem))]
public sealed class VelocityMemorySystem : EntitySystem
{
    private Query _bodies;

    protected override void OnCreate() => _bodies = World.Query(Query.With<LastVelocity, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _bodies.GetChunks())
        {
            var last = chunk.GetSpan<LastVelocity>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
                last[i].Value = velocities[i].Value;
        }
    }
}

/// <summary>
/// Damages blocks by their jolt in the last step (<see cref="Impact"/>) and breaks those with no health left. Does
/// nothing for the first <see cref="SettleSeconds"/>, while freshly placed towers settle.
/// </summary>
[UpdateAfter(typeof(PhysicsSystem))]
public sealed class ImpactDamageSystem(PhysicsSystem physics, Wrecker wrecker) : EntitySystem
{
    public const float SettleSeconds = 1f;

    private Query _blocks;
    private float _settleLeft = SettleSeconds;

    protected override void OnCreate() =>
        _blocks = World.Query(Query.With<Block, LastVelocity, Velocity>().With<RigidBody, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (_settleLeft > 0f)
        {
            _settleLeft -= deltaSeconds;
            return;
        }

        var gravityStep = physics.Gravity * deltaSeconds;
        foreach (var chunk in _blocks.GetChunks())
        {
            var blocks = chunk.GetSpan<Block>();
            var last = chunk.GetSpan<LastVelocity>();
            var velocities = chunk.GetSpan<Velocity>();
            var bodies = chunk.GetSpan<RigidBody>();
            var positions = chunk.GetSpan<Position>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                if (bodies[i].IsSleeping || blocks[i].IsWrecked)
                    continue;

                var jolt = Impact.Jolt(last[i].Value, velocities[i].Value, gravityStep * bodies[i].GravityScale);
                var damage = Impact.Damage(jolt);
                if (damage <= 0f)
                    continue;

                blocks[i].Health -= damage;
                if (blocks[i].Health <= 0f)
                    wrecker.Wreck(Commands, entities[i], ref blocks[i], positions[i].Value);
            }
        }
    }
}

/// <summary>
/// Ages shots and removes stones that came to rest or are too old. A bomb goes off when it touches a body (a contact
/// begin event), lands on the ground (a jolt: tiles send no events) or its fuse runs out. The blast finds what is near
/// with <see cref="IPhysicsQueries.OverlapCircle"/>, damages blocks and throws everything away from its centre.
/// </summary>
[UpdateAfter(typeof(PhysicsSystem))]
public sealed class ShotSystem(PhysicsSystem physics, Wrecker wrecker, EffectSpawner effects) : EntitySystem
{
    public const float StoneLifetime = 8f;
    public const float BombFuse = 4f;
    private const int MaxBlastHits = 128;

    private Query _shots;

    protected override void OnCreate() =>
        _shots = World.Query(Query.With<Shot, Position, Velocity>().With<LastVelocity, RigidBody>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var contact in physics.ContactBeginEvents)
        {
            TryDetonate(contact.A);
            TryDetonate(contact.B);
        }

        var gravityStep = physics.Gravity * deltaSeconds;
        foreach (var chunk in _shots.GetChunks())
        {
            var shots = chunk.GetSpan<Shot>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            var last = chunk.GetSpan<LastVelocity>();
            var bodies = chunk.GetSpan<RigidBody>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var shot = ref shots[i];
                if (shot.IsSpent)
                    continue;

                shot.Age += deltaSeconds;
                if (shot.Kind == ShotKind.Bomb)
                {
                    var jolt = Impact.Jolt(last[i].Value, velocities[i].Value, gravityStep);
                    if (jolt > Impact.BombTriggerSpeed || shot.Age >= BombFuse)
                        Detonate(entities[i], ref shot, positions[i].Value);
                }
                else if (bodies[i].IsSleeping || shot.Age >= StoneLifetime)
                {
                    shot.IsSpent = true;
                    Commands.Destroy(entities[i]);
                }
            }
        }
    }

    private void TryDetonate(Entity entity)
    {
        if (!World.IsAlive(entity) || !World.Has<Shot>(entity))
            return;

        ref var shot = ref World.Get<Shot>(entity);
        if (shot.Kind == ShotKind.Bomb && !shot.IsSpent)
            Detonate(entity, ref shot, World.Get<Position>(entity).Value);
    }

    private void Detonate(Entity bomb, ref Shot shot, Vector2 center)
    {
        shot.IsSpent = true;
        Commands.Destroy(bomb);
        effects.Explosion(Commands, center);

        Span<Entity> hits = stackalloc Entity[MaxBlastHits];
        var count = physics.OverlapCircle(center, Impact.BlastRadius, Layers.Blocks | Layers.Shots, hits);
        for (var i = 0; i < count; i++)
        {
            var entity = hits[i];
            if (entity == bomb || !World.Has<RigidBody>(entity))
                continue;

            ref var body = ref World.Get<RigidBody>(entity);
            if (body.Type != BodyType.Dynamic)
                continue;

            var position = World.Get<Position>(entity).Value;
            var away = position - center;
            var distance = away.Length();
            var falloff = Impact.BlastFalloff(distance);
            var direction = distance > .01f ? away / distance : -Vector2.UnitY;
            World.Get<Velocity>(entity).Value += direction * (Impact.BlastPush * falloff) - Vector2.UnitY * 5f;
            body.IsSleeping = false;

            if (!World.Has<Block>(entity))
                continue;

            ref var block = ref World.Get<Block>(entity);
            block.Health -= Impact.BlastDamage * falloff;
            if (block.Health <= 0f)
                wrecker.Wreck(Commands, entity, ref block, position);
        }
    }
}

/// <summary>Sinks blocks and shots that fall into the water (sensor begin events).</summary>
[UpdateAfter(typeof(PhysicsSystem))]
public sealed class WaterSystem(IPhysicsEvents events, Wrecker wrecker, EffectSpawner effects) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var sensor in events.SensorBeginEvents)
        {
            var visitor = sensor.Visitor;
            if (!World.Has<Water>(sensor.Sensor) || !World.IsAlive(visitor))
                continue;

            if (World.Has<Block>(visitor))
            {
                wrecker.Wreck(Commands, visitor, ref World.Get<Block>(visitor), World.Get<Position>(visitor).Value,
                    sinks: true);
            }
            else if (World.Has<Shot>(visitor))
            {
                ref var shot = ref World.Get<Shot>(visitor);
                if (shot.IsSpent)
                    continue;

                shot.IsSpent = true;
                Commands.Destroy(visitor);
                effects.Splash(Commands, World.Get<Position>(visitor).Value);
            }
        }
    }
}
