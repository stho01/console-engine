using System;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>How a body moves. The same three types as Box2D, Unity and Godot.</summary>
public enum BodyType : byte
{
    /// <summary>Never moves. Infinite mass. Two static bodies are never tested against each other.</summary>
    Static,

    /// <summary>Moved by its <c>Velocity</c> only: no gravity, and contacts never push it. Infinite mass.</summary>
    Kinematic,

    /// <summary>Moved by its <c>Velocity</c>, gravity and (from phase 3) contacts.</summary>
    Dynamic
}

/// <summary>
/// Makes an entity a physics body. A <see cref="Collider"/> gives it a shape. Moving bodies also need a
/// <c>Velocity</c>, and <see cref="PhysicsSystem"/> moves them instead of <c>MovementSystem</c>. Create it with
/// <see cref="Dynamic"/>, <see cref="Kinematic"/> or <see cref="Static"/>: <c>default</c> is a static body.
/// </summary>
public struct RigidBody
{
    //**********************************************************
    //** fields
    //**********************************************************

    public BodyType Type;

    /// <summary>1 / mass. 0 for static and kinematic bodies.</summary>
    public float InverseMass;

    /// <summary>Multiplies <see cref="PhysicsSystem.Gravity"/> for this body. Dynamic bodies only.</summary>
    public float GravityScale;

    /// <summary>
    /// Slows the body down, per second: each step multiplies the velocity by <c>1 / (1 + step * LinearDamping)</c>
    /// (as Box2D). Dynamic bodies only.
    /// </summary>
    public float LinearDamping;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static RigidBody Dynamic(float mass = 1f, float gravityScale = 1f, float linearDamping = 0f)
    {
        if (!(mass > 0f) || !float.IsFinite(mass))
            throw new ArgumentOutOfRangeException(nameof(mass), mass, "Must be a positive, finite number.");

        return new RigidBody
        {
            Type = BodyType.Dynamic,
            InverseMass = 1f / mass,
            GravityScale = gravityScale,
            LinearDamping = linearDamping
        };
    }

    public static RigidBody Kinematic() => new() { Type = BodyType.Kinematic };

    public static RigidBody Static() => new() { Type = BodyType.Static };
}
