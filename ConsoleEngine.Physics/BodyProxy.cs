using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// One collider as the physics step sees it, copied out of the chunks at the start of the step. Everything the
/// broadphase and narrowphase read for a pair is in this one struct (32 bytes), so a pair test touches two cache lines
/// at most.
/// </summary>
internal struct BodyProxy
{
    /// <summary>Centre of the shape in world cells (position + collider offset) at the start of the step.</summary>
    public Vector2 Center;

    /// <summary>Half size of the shape's bounding box. For a circle both are the radius.</summary>
    public Vector2 Extents;

    public uint Category;
    public uint Mask;

    /// <summary>
    /// How far the body can move during this step. The broadphase grows the bounding box by it, so pairs that are
    /// about to touch get a (speculative) contact before they overlap.
    /// </summary>
    public float Margin;

    public ShapeType Shape;
    public BodyType Type;
    public bool IsSensor;

    /// <summary>For circles.</summary>
    public readonly float Radius => Extents.X;

    /// <summary>Half size of the box the broadphase uses: the shape's, grown by <see cref="Margin"/>.</summary>
    public readonly Vector2 BoundsExtents => new(Extents.X + Margin, Extents.Y + Margin);
}
