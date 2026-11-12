using System;
using ConsoleEngine.Physics;

namespace PhysicsShowcase.Components;

/// <summary>The colour of an awake body. <c>TintSystem</c> draws it dark grey while it sleeps.</summary>
public struct Tint(ConsoleColor color)
{
    public ConsoleColor Color = color;
}

/// <summary>Tag: a body the player dropped. <c>R</c> removes all of them.</summary>
public struct Dropped;

/// <summary>Moves a kinematic body back and forth between <see cref="MinX"/> and <see cref="MaxX"/>.</summary>
public struct Patrol(float minX, float maxX, float speed)
{
    public float MinX = minX;
    public float MaxX = maxX;

    /// <summary>Cells per second.</summary>
    public float Speed = speed;
}

/// <summary>
/// The player character: a 1×1 box moved by <c>CharacterMover.MoveAndSlide</c>, not by the physics. Input is latched
/// every frame and used in the fixed step.
/// </summary>
public struct Player
{
    public float Velocity;
    public float FallSpeed;

    /// <summary>-1 (A), 1 (D) or 0, from the latest frame.</summary>
    public float RunDirection;

    public bool JumpRequested;

    /// <summary>1 (right) or -1 (left): where the laser points.</summary>
    public float Facing;

    /// <summary>What the last move ran into.</summary>
    public SlideCollisions Collisions;
}
