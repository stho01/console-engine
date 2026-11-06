using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Platformer.Ecs.Components;
using Platformer.Ecs.Utils;

namespace Platformer.Ecs.Systems;

/// <summary>
/// Gravity, jumps, drag and movement with tile collisions for every <see cref="PlayerBody"/>. The engine's
/// <c>MovementSystem</c> isn't used for these entities: moving and resolving collisions must happen together.
/// </summary>
public sealed class PlayerPhysicsSystem(TileMap map) : EntitySystem
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

    /// <summary>
    /// Longest move between two collision checks, in cells. Pushing out of a wall only works for overlaps below
    /// half a cell, so a slow frame is split into several steps instead of tunnelling into the wall.
    /// </summary>
    private const float MaxStep = .5f;

    /// <summary>How far corner probes are moved inwards, so touching a wall on one axis doesn't block the other.</summary>
    private const float CornerInset = .05f;

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
        var running = body.Acceleration.X != 0f;
        body.Acceleration.Y += Gravity;

        if (body.JumpRequested && !body.IsAirborne)
        {
            velocity.Y = -body.JumpSpeed;
            body.IsAirborne = true;
            body.JumpRequested = false;
        }

        velocity -= body.Drag * velocity * deltaSeconds;
        velocity += body.Acceleration * deltaSeconds;
        body.Acceleration = Vector2.Zero;

        Move(ref body, ref position, ref velocity, ref probe, deltaSeconds);

        if (!running && MathF.Abs(velocity.X) < MinSpeed)
            velocity.X = 0f;
    }

    /// <summary>
    /// Moves by <c>velocity * delta</c> in steps of at most <see cref="MaxStep"/>, checking walls after each step.
    /// </summary>
    private void Move(ref PlayerBody body, ref Vector2 position, ref Vector2 velocity, ref CollisionProbe probe,
        float deltaSeconds)
    {
        var displacement = velocity * deltaSeconds;
        var longest = MathF.Max(MathF.Abs(displacement.X), MathF.Abs(displacement.Y));
        var steps = Math.Max(1, (int)MathF.Ceiling(longest / MaxStep));
        var step = displacement / steps;

        for (var s = 0; s < steps; s++)
        {
            position = ResolveCollisions(ref body, position, step, ref velocity, ref probe);

            // A blocked axis stops moving for the rest of the frame.
            if (velocity.X == 0f) step.X = 0f;
            if (velocity.Y == 0f) step.Y = 0f;
        }
    }

    /// <summary>
    /// Same rules as <c>Player.CheckCollisions</c> in <c>Examples/Platformer</c>: a wall below lands the body, a wall
    /// above or to the side stops that axis, and the position is snapped to whole cells out of the wall.
    /// <para>
    /// Change 1: the axes are checked one after the other. The vertical probes use the x before the step, the
    /// horizontal probes the y after it was snapped. The original probed both at the unsnapped target, so a step
    /// deeper than <see cref="CornerInset"/> into one wall also counted for the other axis: on a slow PC (roughly
    /// below 60 FPS) the floor blocked walking and pressing into a side wall let the player climb it.
    /// </para>
    /// <para>
    /// Change 2: moving right checks the two right corners, moving left the two left corners. The original checked
    /// right-bottom + left-bottom and right-top + left-top, so a player touching a wall on the right couldn't walk
    /// left away from it.
    /// </para>
    /// </summary>
    private Vector2 ResolveCollisions(ref PlayerBody body, Vector2 from, Vector2 step, ref Vector2 velocity,
        ref CollisionProbe probe)
    {
        var x = from.X + step.X;
        var y = from.Y + step.Y;
        probe.Bounds = new RectangleF(x, y, body.Size.X, body.Size.Y);

        var left = from.X;
        var right = from.X + body.Size.X;
        var vertical = probe.Vertical = new CornerTiles
        {
            RightBottom = map.GetTile(right - CornerInset, y + body.Size.Y),
            LeftBottom = map.GetTile(left + CornerInset, y + body.Size.Y),
            RightTop = map.GetTile(right - CornerInset, y),
            LeftTop = map.GetTile(left + CornerInset, y)
        };

        if (velocity.Y > 0 && (TileMap.IsWall(vertical.RightBottom) || TileMap.IsWall(vertical.LeftBottom)))
        {
            y = (int)y;
            body.IsAirborne = false;
            velocity.Y = 0f;
        }
        else if (velocity.Y < 0 && (TileMap.IsWall(vertical.RightTop) || TileMap.IsWall(vertical.LeftTop)))
        {
            y = (int)(y + .5f);
            velocity.Y = 0f;
        }

        var top = y;
        var bottom = y + body.Size.Y;
        var horizontal = probe.Horizontal = new CornerTiles
        {
            RightBottom = map.GetTile(x + body.Size.X, bottom - CornerInset),
            LeftBottom = map.GetTile(x, bottom - CornerInset),
            RightTop = map.GetTile(x + body.Size.X, top + CornerInset),
            LeftTop = map.GetTile(x, top + CornerInset)
        };

        if (velocity.X > 0 && (TileMap.IsWall(horizontal.RightBottom) || TileMap.IsWall(horizontal.RightTop)))
        {
            x = (int)x;
            velocity.X = 0f;
        }
        else if (velocity.X < 0 && (TileMap.IsWall(horizontal.LeftBottom) || TileMap.IsWall(horizontal.LeftTop)))
        {
            x = (int)(x + .5f);
            velocity.X = 0f;
        }

        return new Vector2(x, y);
    }
}
