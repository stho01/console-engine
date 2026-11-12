namespace SkyStack.Components;

/// <summary>
/// The player. Its <c>Position</c> is the left edge (x) and the soles of the feet (y) of a body
/// <c>ClimbPhysics.BodyWidth</c> columns wide and <c>ClimbPhysics.BodyHeight</c> rows tall; its <c>Velocity</c> is in
/// columns and rows per second.
/// </summary>
public struct Climber
{
    /// <summary>-1 (left held), 1 (right held) or 0, from the latest frame.</summary>
    public float RunDirection;

    /// <summary>A jump key is held: the climber jumps whenever it stands on a floor.</summary>
    public bool JumpHeld;

    /// <summary>Standing on <see cref="Floor"/>.</summary>
    public bool IsGrounded;

    /// <summary>The floor stood on, or the last one landed on while in the air.</summary>
    public int Floor;

    /// <summary>Time left to show the spark of the last wall bounce.</summary>
    public float SparkSeconds;

    /// <summary>-1 when the last bounce was off the left wall, 1 off the right one.</summary>
    public int SparkSide;
}
