using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// One collider as the physics step sees it, copied out of the chunks at the start of the step. Everything the
/// broadphase and narrowphase read for a pair is in this one struct (32 bytes), so a pair test touches two cache lines
/// at most.
/// </summary>
internal struct BodyProxy
{
    /// <summary>Centre of the shape in world cells (position + collider offset).</summary>
    public Vector2 Center;

    /// <summary>Half size of the bounding box. For a circle both are the radius.</summary>
    public Vector2 Extents;

    public uint Category;
    public uint Mask;
    public ShapeType Shape;
    public BodyType Type;
    public bool IsSensor;

    public readonly Vector2 Min => Center - Extents;
    public readonly Vector2 Max => Center + Extents;

    /// <summary>For circles.</summary>
    public readonly float Radius => Extents.X;
}
