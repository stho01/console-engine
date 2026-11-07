using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using PhysicsShowcase.Components;

namespace PhysicsShowcase.Systems;

/// <summary>
/// Turns a patrolling kinematic body around at the ends of its range by setting its velocity; the physics moves it.
/// Runs in the fixed step, before <see cref="PhysicsSystem"/>.
/// </summary>
[UpdateBefore(typeof(PhysicsSystem))]
public sealed class PatrolSystem : EntitySystem
{
    private Query _patrols;

    protected override void OnCreate() => _patrols = World.Query(Query.With<Patrol, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _patrols.GetChunks())
        {
            var patrols = chunk.GetSpan<Patrol>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (positions[i].Value.X <= patrols[i].MinX)
                    velocities[i].Value.X = patrols[i].Speed;
                else if (positions[i].Value.X >= patrols[i].MaxX)
                    velocities[i].Value.X = -patrols[i].Speed;
            }
        }
    }
}

/// <summary>
/// Moves the player with <see cref="CharacterMover.MoveAndSlide"/> against tiles and solid bodies: run, jump, fall with
/// the world's gravity (also when it points up). The player's own collider makes it a static obstacle for bodies; the
/// mover ignores it.
/// </summary>
public sealed class PlayerMoveSystem(PhysicsSystem physics) : EntitySystem
{
    private const float RunSpeed = 18f;
    private const float RunAcceleration = 120f;
    private const float JumpSpeed = 28f;
    private static readonly Vector2 HalfSize = new(.5f, .5f);

    private Query _players;

    protected override void OnCreate() => _players = World.Query(Query.With<Player, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var gravity = physics.Gravity;
        var down = gravity == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(gravity);
        foreach (var chunk in _players.GetChunks())
        {
            var players = chunk.GetSpan<Player>();
            var positions = chunk.GetSpan<Position>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var player = ref players[i];
                var target = player.RunDirection * RunSpeed;
                var step = RunAcceleration * deltaSeconds;
                player.Velocity += Math.Clamp(target - player.Velocity, -step, step);

                var onFloor = (player.Collisions & SlideCollisions.Floor) != 0;
                if (player.JumpRequested && onFloor)
                    player.FallSpeed = -JumpSpeed;
                player.JumpRequested = false;
                player.FallSpeed += gravity.Length() * deltaSeconds;

                var velocity = new Vector2(player.Velocity, 0f) + down * player.FallSpeed;
                var center = positions[i].Value;
                player.Collisions = CharacterMover.MoveAndSlide(physics, ref center, ref velocity, HalfSize,
                    deltaSeconds, -down, Layers.Tiles | Layers.Solid, entities[i]);
                positions[i].Value = center;
                // Gravity only ever points straight down or up here, so running stays along x.
                player.Velocity = velocity.X;
                player.FallSpeed = Vector2.Dot(velocity, down);
            }
        }
    }
}

/// <summary>
/// Reads the physics events after the step: counts the bodies inside the sensor zone (begin minus end) and every contact
/// that begins.
/// </summary>
public sealed class EventCounterSystem(IPhysicsEvents events, ShowcaseState state) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var sensor in events.SensorBeginEvents)
        {
            if (sensor.Sensor == state.Zone)
                state.InZone++;
        }

        foreach (var sensor in events.SensorEndEvents)
        {
            if (sensor.Sensor == state.Zone)
                state.InZone = Math.Max(0, state.InZone - 1);
        }

        state.Impacts += events.ContactBeginEvents.Length;
    }
}
