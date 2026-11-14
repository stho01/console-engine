using System;

namespace Kartster.Driving;

/// <summary>
/// What one axle does lengthways, after Kartster's <c>KartTraction</c>: the force that makes the wheels match the
/// road this step, capped by the tyres; past the cap the wheels spin up. Whatever grip goes lengthways is gone
/// sideways (friction circle, <see cref="GetSidewaysGripShare"/>).
/// </summary>
public static class KartTraction
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float WheelMassKilograms = 20f;
    public const float MinimumSidewaysGripShare = 0.22f;

    /// <summary>Metres per second the driven wheels run ahead of the road before they count as spinning.</summary>
    public const float SpinSpeed = 1.5f;

    public const float StraightBrakeGripUse = 0.9f;
    public const float CorneringBrakeGripUse = 0.5f;
    public const float CorneringBrakeSteering = 0.5f;

    /// <summary>How far past a gear's top speed the road must be before the gear holds the wheels back.</summary>
    public const float OverRevShare = 1.05f;

    public const float HandbrakeEngineShare = 0.5f;

    /// <summary>Share of <see cref="KartTuning.TyreFriction"/> a locked tyre drags with, and keeps sideways.</summary>
    public const float LockedGripScale = 0.25f;

    /// <param name="Force">Along the wheel, newtons.</param>
    /// <param name="WheelSpeed">At the tread, metres per second.</param>
    /// <param name="SidewaysGripShare">Sideways grip left, in [0, 1].</param>
    public readonly record struct AxleResult(float Force, float WheelSpeed, float SidewaysGripShare);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>A driven axle.</summary>
    public static AxleResult Step(float driveForce, float wheelSpeed, float roadSpeed, float maximumForce,
        float deltaSeconds, float minimumSidewaysShare)
    {
        // A driven wheel behind the road is a rolling wheel the kart has just passed: the road brings it along.
        var rollingSpeed = driveForce >= 0f ? MathF.Max(wheelSpeed, roadSpeed) : wheelSpeed;
        var spinSpeed = MathF.Max(rollingSpeed - roadSpeed, 0f);
        var demandedForce = driveForce + spinSpeed * WheelMassKilograms / deltaSeconds;
        var force = Math.Clamp(demandedForce, -maximumForce, maximumForce);
        var nextWheelSpeed = force == demandedForce
            ? roadSpeed
            : rollingSpeed + (driveForce - force) * deltaSeconds / WheelMassKilograms;
        return new AxleResult(force, nextWheelSpeed,
            GetSidewaysGripShare(demandedForce, maximumForce, minimumSidewaysShare));
    }

    /// <summary>A braking axle. The wheels keep rolling; the brakes use less grip while steering.</summary>
    public static AxleResult Brake(float brakeForce, float roadSpeed, float maximumForce, float steering)
    {
        var cornering = MathF.Min(MathF.Abs(steering) / CorneringBrakeSteering, 1f);
        var gripUse = StraightBrakeGripUse + (CorneringBrakeGripUse - StraightBrakeGripUse) * cornering;
        var force = -MathF.Sign(roadSpeed) * MathF.Min(brakeForce, maximumForce * gripUse);
        return new AxleResult(force, roadSpeed, GetSidewaysGripShare(force, maximumForce, MinimumSidewaysGripShare));
    }

    /// <summary>An axle with its wheels held at a speed (handbrake, or a gear too low): the tyres drag.</summary>
    public static AxleResult Lock(float wheelSpeed, float roadSpeed, float slidingForce, float mass, float deltaSeconds)
    {
        // Never so much that it takes the kart past the wheels within the step.
        var force = Math.Clamp((wheelSpeed - roadSpeed) * mass / deltaSeconds, -slidingForce, slidingForce);
        return new AxleResult(force, wheelSpeed, LockedGripScale);
    }

    public static float GetSidewaysGripShare(float demandedForce, float maximumForce, float minimumShare)
    {
        var used = maximumForce > 0f ? MathF.Min(MathF.Abs(demandedForce) / maximumForce, 1f) : 1f;
        return MathF.Max(MathF.Sqrt(1f - used * used), minimumShare);
    }
}
