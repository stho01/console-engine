using System;
using Kartster.Driving;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster;

/// <summary>
/// The one place that converts between the physics frame (<c>ConsoleEngine.Physics</c>: x right, y down on screen,
/// angles clockwise on screen, 0 facing -y) and the kart frame (<see cref="KartBody"/>: x = world X, y = world Z,
/// angles positive turning left, 0 facing -Z). Physics y is world Z, so the screen shows the track from above and
/// clockwise on screen is clockwise seen from above: positions, velocities and forces are the same, angles, angular
/// velocities and torques change sign.
/// </summary>
public static class KartFrame
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Kart heading from a physics <c>Rotation.Radians</c>.</summary>
    public static float ToHeading(float radians) => -radians;

    /// <summary>Physics <c>Rotation.Radians</c> from a kart heading.</summary>
    public static float ToRadians(float heading) => -heading;

    /// <summary>The heading of a kart facing <paramref name="forward"/> (world axes, Y ignored).</summary>
    public static float HeadingOf(System.Numerics.Vector3 forward) => MathF.Atan2(-forward.X, -forward.Z);

    /// <summary>The kart state of a physics body.</summary>
    public static KartBody ToBody(XnaVector2 position, float radians, XnaVector2 velocity, float angularVelocity) =>
        new()
        {
            Position = new System.Numerics.Vector2(position.X, position.Y),
            Heading = ToHeading(radians),
            Velocity = new System.Numerics.Vector2(velocity.X, velocity.Y),
            AngularVelocity = -angularVelocity,
        };

    /// <summary>A kart force (x, z) is the physics force (x, y): the same axes.</summary>
    public static XnaVector2 ToPhysicsForce(System.Numerics.Vector2 force) => new(force.X, force.Y);

    /// <summary>Kart torque (positive = left) as physics torque (positive = clockwise).</summary>
    public static float ToPhysicsTorque(float torque) => -torque;
}
