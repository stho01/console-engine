using System;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Spatial queries against the colliders and the <see cref="TileGrid"/>, as Box2D's world queries. They see the world as
/// it was at the end of the last physics step: bodies created, destroyed or moved by game code since then show up after
/// the next step. Sensors are never reported. <c>mask</c> parameters are category bits: a collider (or the
/// tile grid) is reported if its category has a bit in the mask.
/// </summary>
public interface IPhysicsQueries
{
    /// <summary>
    /// The closest collider or solid tile along <c>origin + fraction * translation</c>, fraction 0 to 1. Shapes that
    /// contain the origin are ignored.
    /// </summary>
    bool Raycast(Vector2 origin, Vector2 translation, uint mask, out CastHit hit);

    /// <summary>
    /// The first collider or solid tile an axis-aligned box hits when moved by <paramref name="translation"/>. A shape
    /// the box already overlaps is hit at fraction 0 if the box moves further into it, and ignored if it moves out.
    /// </summary>
    bool CastBox(Vector2 center, Vector2 halfExtents, Vector2 translation, uint mask, Entity ignore, out CastHit hit);

    /// <summary>Writes the entities whose colliders overlap the box into <paramref name="results"/>; returns how many.
    /// Stops when <paramref name="results"/> is full. Tiles aren't included: use <see cref="TileGrid.Overlaps"/>.</summary>
    int OverlapBox(Vector2 center, Vector2 halfExtents, uint mask, Span<Entity> results);

    /// <summary>As <see cref="OverlapBox"/>, for a circle.</summary>
    int OverlapCircle(Vector2 center, float radius, uint mask, Span<Entity> results);
}

/// <summary>Result of <see cref="IPhysicsQueries.Raycast"/> and <see cref="IPhysicsQueries.CastBox"/>.</summary>
public readonly struct CastHit(Entity entity, Point tile, Vector2 point, Vector2 normal, float fraction)
{
    /// <summary>The collider's entity, or <see cref="Entity.Null"/> for a tile.</summary>
    public Entity Entity { get; } = entity;

    public bool IsTile => Entity == Entity.Null;

    /// <summary>Column and row of the tile hit. Only when <see cref="IsTile"/>.</summary>
    public Point Tile { get; } = tile;

    /// <summary>
    /// Where the ray (or the cast box's centre) stops: <c>origin + fraction * translation</c>. For a box cast it's the
    /// box's centre at contact, not the contact point.
    /// </summary>
    public Vector2 Point { get; } = point;

    /// <summary>Surface normal at the hit, pointing away from the shape that was hit.</summary>
    public Vector2 Normal { get; } = normal;

    /// <summary>0 to 1 along the translation.</summary>
    public float Fraction { get; } = fraction;
}
