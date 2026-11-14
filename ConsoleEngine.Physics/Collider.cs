using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

public enum ShapeType : byte
{
    Circle,

    /// <summary>A box, turned with the body's <c>Rotation</c> if it has one.</summary>
    Box,

    /// <summary>A convex polygon from the system's <see cref="PolygonLibrary"/>.</summary>
    Polygon
}

/// <summary>
/// The shape of a body and what it collides with, around the entity's <c>Position</c>, in cells. Create it with
/// <see cref="Circle"/>, <see cref="Box"/> or <see cref="Polygon"/> and change the rest with <c>with</c>:
/// <c>Collider.Circle(.5f) with { IsSensor = true }</c>. An entity with a collider but no <see cref="RigidBody"/> is a
/// static body. If the entity has a <c>Rotation</c>, the shape (and its <see cref="Offset"/>) turns with it around
/// <c>Position</c>.
/// <para>
/// Two colliders are tested only if each one's <see cref="Category"/> is in the other's <see cref="Mask"/> (Box2D's
/// filter rule). The factories put the collider in category 1, let it collide with every category, and give it
/// <see cref="DefaultFriction"/> and no restitution.
/// </para>
/// </summary>
public struct Collider
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const uint DefaultCategory = 1u;
    public const uint AllCategories = uint.MaxValue;

    /// <summary>Box2D's default.</summary>
    public const float DefaultFriction = .6f;

    public ShapeType Shape;

    /// <summary>Half width and half height. <see cref="ShapeType.Box"/> only.</summary>
    public Vector2 HalfExtents;

    /// <summary><see cref="ShapeType.Circle"/> only.</summary>
    public float Radius;

    /// <summary>Id in the <see cref="PhysicsSystem.Polygons"/> library. <see cref="ShapeType.Polygon"/> only.</summary>
    public int PolygonId;

    /// <summary>
    /// Centre of a circle or box relative to the entity's position. Polygons have their vertices relative to the position.
    /// </summary>
    public Vector2 Offset;

    /// <summary>The categories this collider belongs to, as bits.</summary>
    public uint Category;

    /// <summary>The categories this collider collides with, as bits.</summary>
    public uint Mask;

    /// <summary>
    /// Reports overlaps as sensor events and never takes part in contacts. Two sensors don't detect each other.
    /// </summary>
    public bool IsSensor;

    /// <summary>
    /// Coulomb friction, usually 0 to 1. A pair uses <c>sqrt(a * b)</c>, so 0 on either side means no friction.
    /// </summary>
    public float Friction;

    /// <summary>
    /// Bounciness, 0 (no bounce) to 1 (keeps its speed). A pair uses the larger of the two. Only for impacts faster than
    /// <see cref="PhysicsSystem.RestitutionThreshold"/>.
    /// </summary>
    public float Restitution;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static Collider Circle(float radius, Vector2 offset = default) => new()
    {
        Shape = ShapeType.Circle,
        Radius = radius,
        Offset = offset,
        Category = DefaultCategory,
        Mask = AllCategories,
        Friction = DefaultFriction
    };

    public static Collider Box(Vector2 halfExtents, Vector2 offset = default) => new()
    {
        Shape = ShapeType.Box,
        HalfExtents = halfExtents,
        Offset = offset,
        Category = DefaultCategory,
        Mask = AllCategories,
        Friction = DefaultFriction
    };

    /// <param name="polygonId">
    /// From <see cref="PolygonLibrary.Add"/> of the <see cref="PhysicsSystem"/> it's used with.
    /// </param>
    public static Collider Polygon(int polygonId) => new()
    {
        Shape = ShapeType.Polygon,
        PolygonId = polygonId,
        Category = DefaultCategory,
        Mask = AllCategories,
        Friction = DefaultFriction
    };
}
