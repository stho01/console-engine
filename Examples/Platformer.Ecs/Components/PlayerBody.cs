using Microsoft.Xna.Framework;

namespace Platformer.Ecs.Components;

/// <summary>A player-controlled body that runs, jumps, falls and collides with the level's tile grid.</summary>
public struct PlayerBody
{
    /// <summary>Collision box size in cells, from the entity's <c>Position</c> (top-left).</summary>
    public Vector2 Size;

    /// <summary>
    /// The exact position minus <c>Position</c>, -0.5 to 0.5 per axis. <c>Position</c> stays on whole cells, so the
    /// sprite, the camera and the tiles are rounded the same way: a player standing 0.01 above the floor (the mover's
    /// skin) is drawn on it, not a row higher.
    /// </summary>
    public Vector2 SubCell;

    /// <summary>Horizontal acceleration while A or D is held on the ground, cells/s².</summary>
    public float GroundAcceleration;

    /// <summary>Horizontal acceleration while A or D is held in the air, cells/s².</summary>
    public float AirAcceleration;

    /// <summary>Upward speed set by a jump, cells/s.</summary>
    public float JumpSpeed;

    /// <summary>Fraction of velocity lost per second, per axis.</summary>
    public Vector2 Drag;

    /// <summary>
    /// -1 (A held), 1 (D held) or 0, from the latest frame. The fixed step turns it into an acceleration; it isn't summed
    /// over frames, so the frame rate doesn't change how hard the player runs.
    /// </summary>
    public float RunDirection;

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
