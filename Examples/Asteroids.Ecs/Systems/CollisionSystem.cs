using Asteroids.Ecs.Components;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;

namespace Asteroids.Ecs.Systems;

/// <summary>A bullet that touches an asteroid destroys both.</summary>
public sealed class CollisionSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _bullets;
    private Query _asteroids;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _bullets = World.Query(Query.With<Position, Collider, Bullet>());
        _asteroids = World.Query(Query.With<Position, Collider, Asteroid>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var bulletChunk in _bullets.GetChunks())
        {
            var bulletPositions = bulletChunk.GetSpan<Position>();
            var bulletColliders = bulletChunk.GetSpan<Collider>();
            for (var b = 0; b < bulletChunk.Count; b++)
            {
                if (!TryFindHit(bulletPositions[b].Value, bulletColliders[b].Radius, out var asteroid))
                    continue;

                // Two bullets can hit the same asteroid in one frame. Playback skips the second destroy.
                Commands.Destroy(bulletChunk.Entities[b]);
                Commands.Destroy(asteroid);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private bool TryFindHit(Vector2 bulletPosition, float bulletRadius, out Entity asteroid)
    {
        foreach (var chunk in _asteroids.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var colliders = chunk.GetSpan<Collider>();
            for (var a = 0; a < chunk.Count; a++)
            {
                var reach = colliders[a].Radius + bulletRadius;
                if (Vector2.DistanceSquared(positions[a].Value, bulletPosition) <= reach * reach)
                {
                    asteroid = chunk.Entities[a];
                    return true;
                }
            }
        }

        asteroid = Entity.Null;
        return false;
    }
}
