using Microsoft.Xna.Framework;

namespace TerraForM.Ecs.Components;

/// <summary>The player's rover. Moves with the engine's <c>Velocity</c>, in cells per second.</summary>
public struct Rover(float maxPower, int sequences)
{
    public float MaxPower = maxPower;

    /// <summary>Can go below 0 (thrust is paid for even without power) and above <see cref="MaxPower"/> (bonus).</summary>
    public double RemainingPower = maxPower;

    public int DamageTaken;
    public int GeneratorsPlanted;
    public int RemainingSequences = sequences;

    /// <summary>Thrust applied this frame, cells/s². Reset after the collision check.</summary>
    public Vector2 Acceleration;

    /// <summary>Where the rover was before this frame's movement. Hitting a rock or crater moves it back here.</summary>
    public Vector2 PositionBeforeMove;

    /// <summary>Set by the previous frame's collision check. <c>Plant</c> only works while it's true.</summary>
    public bool StandingOnPlantSpot;

    public readonly bool PowerDepleted => RemainingPower <= 0;
}
