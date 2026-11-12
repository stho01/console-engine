namespace Asteroids.Ecs;

/// <summary>
/// Collision categories (<c>Collider.Category</c>/<c>Collider.Mask</c> bits). Bullets and asteroids only collide with
/// each other: asteroids pass through asteroids, and the ship has no collider.
/// </summary>
public static class CollisionLayers
{
    public const uint Asteroids = 1u << 0;
    public const uint Bullets = 1u << 1;
}
