using System;
using System.Numerics;

namespace KartViewer;

/// <summary>
/// Arcade driving on a flat plane: throttle, brake (reverse once stopped), steering that turns faster with speed,
/// and grass that holds the kart to a lower top speed. Kartster's axes: +Y up, -Z forward at yaw 0, yaw grows
/// turning left. No grip, slides or collisions.
/// </summary>
public sealed class KartDriving
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float RoadTopSpeed = 28f;      // metres per second, about 100 km/h
    public const float GrassTopSpeed = 10f;
    public const float ReverseTopSpeed = 5f;
    private const float Acceleration = 9f;      // metres per second²
    private const float Braking = 20f;
    private const float RollingDrag = 2f;       // with no pedal pressed
    private const float OverSpeedDrag = 15f;    // above the top speed, e.g. driving onto grass
    private const float SteerRate = 1.8f;       // radians per second at full lock and speed
    private const float FullSteerSpeed = 6f;    // below this, steering is weaker (no turning on the spot)

    //**********************************************************
    //** ctor
    //**********************************************************

    public KartDriving(Vector3 position, Vector3 forward)
    {
        Position = position;
        Yaw = MathF.Atan2(-forward.X, -forward.Z);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public Vector3 Position { get; set; }
    public float Yaw { get; private set; }

    /// <summary>Metres per second along <see cref="Forward"/>, negative when reversing.</summary>
    public float Speed { get; private set; }

    public Vector3 Forward => new(-MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="steer">-1 full right, 1 full left.</param>
    public void Update(bool throttle, bool brake, float steer, bool onRoad, float seconds)
    {
        if (throttle)
            Speed += (Speed < 0 ? Braking : Acceleration) * seconds;
        else if (brake)
            Speed -= (Speed > 0 ? Braking : Acceleration) * seconds;
        else
            Speed = MoveTowardsZero(Speed, RollingDrag * seconds);

        var topSpeed = onRoad ? RoadTopSpeed : GrassTopSpeed;
        if (Speed > topSpeed)
            Speed = MathF.Max(topSpeed, Speed - OverSpeedDrag * seconds);
        Speed = MathF.Max(Speed, -ReverseTopSpeed);

        Yaw += steer * SteerRate * Math.Clamp(Speed / FullSteerSpeed, -1, 1) * seconds;
        Position += Forward * (Speed * seconds);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float MoveTowardsZero(float value, float amount) =>
        value > 0 ? MathF.Max(0, value - amount) : MathF.Min(0, value + amount);
}
