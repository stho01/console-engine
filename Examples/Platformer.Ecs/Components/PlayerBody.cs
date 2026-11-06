using Microsoft.Xna.Framework;

namespace Platformer.Ecs.Components;

/// <summary>A player-controlled body that runs, jumps, falls and collides with the <see cref="TileMap"/>.</summary>
public struct PlayerBody
{
    /// <summary>Collision box size in cells, from the entity's <c>Position</c> (top-left).</summary>
    public Vector2 Size;

    /// <summary>Horizontal acceleration while A or D is held on the ground, cells/s².</summary>
    public float GroundAcceleration;

    /// <summary>Horizontal acceleration while A or D is held in the air, cells/s².</summary>
    public float AirAcceleration;

    /// <summary>Upward speed set by a jump, cells/s.</summary>
    public float JumpSpeed;

    /// <summary>Fraction of velocity lost per second, per axis.</summary>
    public Vector2 Drag;

    /// <summary>Sum of the accelerations applied this frame, cells/s². Cleared once it was integrated.</summary>
    public Vector2 Acceleration;

    /// <summary>
    /// Set by a jump, cleared by landing on a wall. Walking off a ledge does not set it (same as
    /// <c>Examples/Platformer</c>), so the player can still jump once after falling off.
    /// </summary>
    public bool IsAirborne;

    /// <summary>
    /// SPACE was pressed. Stays set until the body can jump, so a jump pressed in the air happens on landing.
    /// </summary>
    public bool JumpRequested;
}
