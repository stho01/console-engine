namespace PhysicsShowcase;

/// <summary>
/// Collision categories (<c>Collider.Category</c>/<c>Collider.Mask</c> bits). Ghost balls go through solid bodies but
/// hit tiles and each other; both kinds are seen by the sensor zone.
/// </summary>
public static class Layers
{
    /// <summary>The tile grid's category (<c>TileGrid.Category</c> default).</summary>
    public const uint Tiles = 1u;

    public const uint Solid = 2u;
    public const uint Ghost = 4u;
    public const uint Zone = 8u;

    public const uint SolidMask = Tiles | Solid | Zone;
    public const uint GhostMask = Tiles | Ghost | Zone;
}
