using System;
using System.Numerics;

namespace KartViewer.Driving;

/// <summary>
/// A kart's planar rigid-body state, seen from above. x = world X, y = world Z (+Y is up). At
/// <see cref="Heading"/> 0 the kart faces -Z, so forward = (0, -1); heading grows turning left (counter-clockwise
/// seen from above): forward = (-sin(Heading), -cos(Heading)), right = (cos(Heading), -sin(Heading)).
/// </summary>
public struct KartBody
{
    /// <summary>Centre of mass, metres.</summary>
    public Vector2 Position;

    /// <summary>Radians, positive = turned left of -Z.</summary>
    public float Heading;

    /// <summary>Metres per second.</summary>
    public Vector2 Velocity;

    /// <summary>dHeading/dt, radians per second, positive = turning left.</summary>
    public float AngularVelocity;

    public readonly Vector2 Forward => new(-MathF.Sin(Heading), -MathF.Cos(Heading));
    public readonly Vector2 Right => new(MathF.Cos(Heading), -MathF.Sin(Heading));
}

/// <summary>The driver's inputs, as Kartster reads them.</summary>
public struct KartControls
{
    /// <summary>0..1.</summary>
    public float Throttle;

    /// <summary>0..1. Brakes while moving forwards, reverses once stopped.</summary>
    public float Brake;

    /// <summary>-1..1, positive = right (as Kartster).</summary>
    public float Steer;

    /// <summary>Locks the rear wheels and halves the engine.</summary>
    public bool Handbrake;
}

/// <summary>
/// Net planar force at the centre of mass (newtons, in <see cref="KartBody"/>'s frame) and yaw torque (newton metres,
/// positive = left), held constant over one step.
/// </summary>
public readonly record struct KartForces(Vector2 Force, float Torque);
