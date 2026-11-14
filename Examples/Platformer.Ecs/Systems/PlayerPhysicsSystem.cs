using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using Platformer.Ecs.Components;

namespace Platformer.Ecs.Systems;

/// <summary>
/// Gravity, jumps, drag and running for every <see cref="PlayerBody"/>, then a move through the level with the engine's
/// <see cref="CharacterMover.MoveAndSlide"/> against the tile grid. Runs in the fixed step, after the
/// <see cref="PhysicsSystem"/>; input comes in through <see cref="PlayerBody.RunDirection"/> and
/// <see cref="PlayerBody.JumpRequested"/>, which <see cref="PlayerControlSystem"/> sets every frame.
/// </summary>
public sealed class PlayerPhysicsSystem(IPhysicsQueries physics) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Downward acceleration, cells/s².</summary>
    private const float Gravity = 200f;

    /// <summary>
    /// Below this horizontal speed (cells/s) a body that isn't running stops sliding. The original cut off the whole
    /// velocity below 0.001 cells per frame (1 cell/s at 1000 FPS), even while running or falling, so on a fast PC
    /// a standing player couldn't start walking or falling. Here it only applies to x without run input.
    /// </summary>
    private const float MinSpeed = 1f;

    private Query _bodies;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _bodies = World.Query(Query.With<PlayerBody, Position, Velocity>().With<CollisionProbe>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _bodies.GetChunks())
        {
            var bodies = chunk.GetSpan<PlayerBody>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            var probes = chunk.GetSpan<CollisionProbe>();

            for (var i = 0; i < chunk.Count; i++)
            {
                Simulate(ref bodies[i], ref positions[i].Value, ref velocities[i].Value, ref probes[i],
                    deltaSeconds);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Simulate(ref PlayerBody body, ref Vector2 position, ref Vector2 velocity, ref CollisionProbe probe,
        float deltaSeconds)
    {
        var running = body.RunDirection != 0f;
        var runAcceleration = body.IsAirborne ? body.AirAcceleration : body.GroundAcceleration;
        var acceleration = new Vector2(body.RunDirection * runAcceleration, Gravity);

        if (body.JumpRequested && !body.IsAirborne)
        {
            velocity.Y = -body.JumpSpeed;
            body.IsAirborne = true;
            body.JumpRequested = false;
        }

        velocity -= body.Drag * velocity * deltaSeconds;
        velocity += acceleration * deltaSeconds;

        // The position is the top-left corner on whole cells; the mover works with the exact centre of the box.
        var halfSize = body.Size * .5f;
        var center = position + body.SubCell + halfSize;
        probe.Collisions = CharacterMover.MoveAndSlide(physics, ref center, ref velocity, halfSize, deltaSeconds);
        var exact = center - halfSize;
        position = new Vector2(MathF.Round(exact.X), MathF.Round(exact.Y));
        body.SubCell = exact - position;

        if ((probe.Collisions & SlideCollisions.Floor) != 0)
            body.IsAirborne = false;

        if (!running && MathF.Abs(velocity.X) < MinSpeed)
            velocity.X = 0f;
    }
}
