using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Gravity, drag and movement for particles. <see cref="TileCollider"/> particles land on solid tiles: one falling
/// into a tile is put on top of it and stops (fast ones bounce once, a bit), one flying into a wall loses its
/// sideways speed.
/// </summary>
public sealed class ParticlePhysicsSystem(Mine mine) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float BounceSpeed = 8f;
    private const float Restitution = .35f;

    private Query _particles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _particles = World.Query(Query.With<Particle, Position, Velocity>().With<Physics>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _particles.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            var physics = chunk.GetSpan<Physics>();
            var collides = chunk.Has<TileCollider>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var p = ref positions[i].Value;
                ref var v = ref velocities[i].Value;
                v.Y += physics[i].Gravity * deltaSeconds;
                v *= Math.Max(0f, 1f - physics[i].Drag * deltaSeconds);

                var previous = p;
                p += v * deltaSeconds;
                if (collides)
                    Collide(ref p, ref v, previous);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Collide(ref Vector2 position, ref Vector2 velocity, Vector2 previous)
    {
        var column = TileColumn(position.X);
        var row = TileRow(position.Y);
        if (!mine.IsSolid(column, row))
            return;

        var previousColumn = TileColumn(previous.X);
        var previousRow = TileRow(previous.Y);
        if (previousRow < row && !mine.IsSolid(column, previousRow))
        {
            // Landed on top of the tile.
            position.Y = row * Layout.TileHeight - .01f;
            if (velocity.Y > BounceSpeed)
            {
                velocity.Y = -velocity.Y * Restitution;
                velocity.X *= .6f;
            }
            else
            {
                velocity = Vector2.Zero;
            }

            return;
        }

        if (previousColumn != column && !mine.IsSolid(previousColumn, row))
        {
            // Hit a wall: keep falling, lose the sideways speed.
            position.X = previous.X;
            velocity.X = 0f;
            return;
        }

        // Started inside or hit a ceiling: stay where it was.
        position = previous;
        velocity = Vector2.Zero;
    }

    private static int TileColumn(float x) => (int)MathF.Floor(x / Layout.TileWidth);

    private static int TileRow(float y) => (int)MathF.Floor(y / Layout.TileHeight);
}
