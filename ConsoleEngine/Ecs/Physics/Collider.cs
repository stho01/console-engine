using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Physics;

public enum ShapeType : byte
{
    Circle,

    /// <summary>Axis-aligned box. Bodies don't rotate, so the box never turns.</summary>
    Box
}

/// <summary>
/// The shape of a body and what it collides with, around the entity's <c>Position</c> (plus <see cref="Offset"/>), in
/// cells. Create it with <see cref="Circle"/> or <see cref="Box"/> and change the rest with <c>with</c>:
/// <c>Collider.Circle(.5f) with { IsSensor = true }</c>. An entity with a collider but no <see cref="RigidBody"/> is a
/// static body.
/// <para>
/// Two colliders are tested only if each one's <see cref="Category"/> is in the other's <see cref="Mask"/> (Box2D's
/// filter rule). The factories put the collider in category 1 and let it collide with every category.
/// </para>
/// </summary>
public struct Collider
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const uint DefaultCategory = 1u;
    public const uint AllCategories = uint.MaxValue;

    public ShapeType Shape;

    /// <summary>Half width and half height. <see cref="ShapeType.Box"/> only.</summary>
    public Vector2 HalfExtents;

    /// <summary><see cref="ShapeType.Circle"/> only.</summary>
    public float Radius;

    /// <summary>Centre of the shape relative to the entity's position.</summary>
    public Vector2 Offset;

    /// <summary>The categories this collider belongs to, as bits.</summary>
    public uint Category;

    /// <summary>The categories this collider collides with, as bits.</summary>
    public uint Mask;

    /// <summary>
    /// Reports overlaps as sensor events and never takes part in contacts. Two sensors don't detect each other.
    /// </summary>
    public bool IsSensor;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static Collider Circle(float radius, Vector2 offset = default) => new()
    {
        Shape = ShapeType.Circle,
        Radius = radius,
        Offset = offset,
        Category = DefaultCategory,
        Mask = AllCategories
    };

    public static Collider Box(Vector2 halfExtents, Vector2 offset = default) => new()
    {
        Shape = ShapeType.Box,
        HalfExtents = halfExtents,
        Offset = offset,
        Category = DefaultCategory,
        Mask = AllCategories
    };
}
