namespace Asteroids.Ecs.Components;

/// <summary>A player-controlled ship.</summary>
public struct Ship
{
    /// <summary>Acceleration while thrusting, cells/s².</summary>
    public float Thrust;

    /// <summary>Radians per second.</summary>
    public float SteeringSpeed;

    /// <summary>Fraction of velocity lost per second.</summary>
    public float Drag;
}
