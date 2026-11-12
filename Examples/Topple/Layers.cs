namespace Topple;

/// <summary>
/// Collision categories (<c>Collider.Category</c>/<c>Collider.Mask</c> bits). Debris only hits the ground, so it never
/// pushes a tower; the water sensors only see blocks and shots.
/// </summary>
public static class Layers
{
    /// <summary>The tile grid's category.</summary>
    public const uint Ground = 1u;

    public const uint Blocks = 2u;
    public const uint Shots = 4u;
    public const uint Debris = 8u;
    public const uint Water = 16u;

    public const uint BlockMask = Ground | Blocks | Shots | Water;
    public const uint ShotMask = Ground | Blocks | Shots | Water;
    public const uint DebrisMask = Ground;
    public const uint WaterMask = Blocks | Shots;

    /// <summary>What the aim guide stops at.</summary>
    public const uint Solid = Ground | Blocks;
}
