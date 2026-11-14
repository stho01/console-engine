using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// One collider as the broadphase sees it, copied out of the chunks at the start of the step: its bounding box and
/// filter, 32 bytes. The exact shape for the narrowphase is in a <see cref="BodyShape"/> at the same index.
/// </summary>
internal struct BodyProxy
{
    /// <summary>Centre of the shape's bounding box in world cells, at the start of the step.</summary>
    public Vector2 Center;

    /// <summary>Half size of the shape's bounding box (for a turned shape, of the turned shape).</summary>
    public Vector2 Extents;

    public uint Category;
    public uint Mask;

    /// <summary>
    /// How far the body can move during this step. The broadphase grows the bounding box by it, so pairs that are
    /// about to touch get a (speculative) contact before they overlap.
    /// </summary>
    public float Margin;

    public BodyType Type;
    public bool IsSensor;

    /// <summary>Asleep at the start of the step.</summary>
    public bool IsSleeping;

    /// <summary>Half size of the box the broadphase uses: the shape's, grown by <see cref="Margin"/>.</summary>
    public readonly Vector2 BoundsExtents => new(Extents.X + Margin, Extents.Y + Margin);
}
