using System;
using Microsoft.Xna.Framework;
using Topple.Components;

namespace Topple;

/// <summary>
/// The player's catapult: aim angle, how far the throw is charged and the chosen ammo. The angle is measured up from
/// the horizontal, towards the right; screen y points down.
/// </summary>
public sealed class Catapult(Vector2 pivot)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float MinAngle = 5f * MathF.PI / 180f;
    public const float MaxAngle = 80f * MathF.PI / 180f;

    /// <summary>Radians per second while an aim key is held.</summary>
    public const float TurnSpeed = 50f * MathF.PI / 180f;

    /// <summary>Launch speed, cells/s, with no charge and fully charged.</summary>
    public const float MinSpeed = 16f;
    public const float MaxSpeed = 56f;

    public const float ChargeSeconds = 1.2f;

    /// <summary>Distance from the pivot to where the shot leaves the arm.</summary>
    public const float ArmLength = 4f;

    //**********************************************************
    //** props
    //**********************************************************

    public Vector2 Pivot { get; } = pivot;
    public float Angle { get; private set; } = 40f * MathF.PI / 180f;

    /// <summary>0 to 1.</summary>
    public float Charge { get; private set; }

    public bool IsCharging { get; private set; }
    public ShotKind Ammo { get; set; }

    /// <summary>Seconds until the arm is ready again after a throw.</summary>
    public float Cooldown { get; private set; }

    public Vector2 Direction => new(MathF.Cos(Angle), -MathF.Sin(Angle));
    public Vector2 Muzzle => Pivot + Direction * ArmLength;
    public float Speed => MinSpeed + (MaxSpeed - MinSpeed) * Charge;
    public Vector2 LaunchVelocity => Direction * Speed;
    public int AngleDegrees => (int)MathF.Round(Angle * 180f / MathF.PI);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="direction">1 raises the arm, -1 lowers it.</param>
    public void Turn(float direction, float deltaSeconds) =>
        Angle = Math.Clamp(Angle + direction * TurnSpeed * deltaSeconds, MinAngle, MaxAngle);

    public void Tick(float deltaSeconds) => Cooldown = MathF.Max(0f, Cooldown - deltaSeconds);

    public void ChargeUp(float deltaSeconds)
    {
        IsCharging = true;
        Charge = MathF.Min(1f, Charge + deltaSeconds / ChargeSeconds);
    }

    /// <summary>Lets go: returns the launch velocity and resets the charge.</summary>
    public Vector2 Release(float cooldownSeconds)
    {
        var velocity = LaunchVelocity;
        Cancel();
        Cooldown = cooldownSeconds;
        return velocity;
    }

    public void Cancel()
    {
        IsCharging = false;
        Charge = 0f;
    }

    /// <summary>Where a thrown body is after <paramref name="seconds"/>, without air or collisions.</summary>
    public static Vector2 PointOnArc(Vector2 start, Vector2 velocity, Vector2 gravity, float seconds) =>
        start + velocity * seconds + gravity * (.5f * seconds * seconds);
}
