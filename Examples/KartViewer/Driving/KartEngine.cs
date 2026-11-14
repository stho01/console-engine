using System;

namespace KartViewer.Driving;

/// <summary>
/// Engine, gearbox choice and the flat-road power balance, after Kartster's <c>KartEngine</c>, <c>KartGearbox</c>
/// and <c>KartEffectTuning.GetTopSpeed</c>. Torque is flat up to <see cref="KartTuning.TorquePeakRpm"/>, then
/// constant power, fading to nothing over the last 8 % before the redline.
/// </summary>
public static class KartEngine
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float RevLimitStart = 0.92f;
    public const float EngineBrakeShare = 0.05f;

    /// <summary>Seconds a gear change takes. The engine drives nothing meanwhile.</summary>
    public const float ShiftSeconds = 0.05f;

    private const float ShiftUpMargin = 1.02f;
    private const float ShiftDownMargin = 1.1f;
    private const float ShiftDownLimit = 0.85f;
    private const float RadiansPerSecondPerRpm = MathF.PI / 30f;
    private const float MaximumSearchSpeed = 200f;
    private const int TopSpeedSearchSteps = 24;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static float GetPeakTorque(KartTuning tuning) =>
        MathF.Max(tuning.EnginePowerWatts, 0f) / (tuning.TorquePeakRpm * RadiansPerSecondPerRpm);

    public static float GetTorque(KartTuning tuning, float rpm)
    {
        var torque = MathF.Max(tuning.EnginePowerWatts, 0f)
            / (MathF.Max(rpm, tuning.TorquePeakRpm) * RadiansPerSecondPerRpm);
        var fadeRpm = tuning.RedlineRpm * (1f - RevLimitStart);
        return torque * Math.Clamp((tuning.RedlineRpm - rpm) / fadeRpm, 0f, 1f);
    }

    /// <param name="gear">One-based.</param>
    /// <param name="speed">Road speed of the driven wheels, metres per second.</param>
    public static float GetRpm(KartTuning tuning, int gear, float speed) =>
        MathF.Max(MathF.Abs(speed) * GetWheelRatio(tuning, gear) / RadiansPerSecondPerRpm, tuning.IdleRpm);

    /// <summary>Road speed at the redline in a gear, metres per second.</summary>
    public static float GetTopSpeedInGear(KartTuning tuning, int gear) =>
        tuning.RedlineRpm * RadiansPerSecondPerRpm / GetWheelRatio(tuning, gear);

    /// <summary>Force on the road at full throttle, newtons.</summary>
    public static float GetDriveForce(KartTuning tuning, int gear, float speed) =>
        GetTorque(tuning, GetRpm(tuning, gear, speed)) * GetWheelRatio(tuning, gear);

    /// <summary>Force the shut throttle holds the kart back with, newtons.</summary>
    public static float GetEngineBrakeForce(KartTuning tuning, int gear, float speed)
    {
        var revs = Math.Clamp((GetRpm(tuning, gear, speed) - tuning.IdleRpm) / (tuning.RedlineRpm - tuning.IdleRpm), 0f, 1f);
        return EngineBrakeShare * GetPeakTorque(tuning) * GetWheelRatio(tuning, gear) * revs;
    }

    /// <summary>The gear an automatic wants at a speed, at most one away from <paramref name="gear"/>.</summary>
    public static int SelectGear(KartTuning tuning, int gear, float speed)
    {
        var force = GetDriveForce(tuning, gear, speed);
        if (gear < tuning.GearRatios.Length && GetDriveForce(tuning, gear + 1, speed) > force * ShiftUpMargin)
            return gear + 1;
        if (gear > 1
            && GetRpm(tuning, gear - 1, speed) < tuning.RedlineRpm * ShiftDownLimit
            && GetDriveForce(tuning, gear - 1, speed) > force * ShiftDownMargin)
            return gear - 1;
        return gear;
    }

    /// <summary>Power that holds the kart at a speed against rolling resistance and drag on a flat road, watts.</summary>
    public static float GetPowerNeeded(KartTuning tuning, float speed) =>
        (tuning.RollingResistance * tuning.Mass * speed + tuning.AerodynamicDrag * speed * speed) * speed;

    /// <summary>Where engine power and resistance balance, found by halving the range (as Kartster does).</summary>
    public static float GetTopSpeed(KartTuning tuning)
    {
        var low = 0f;
        var high = MaximumSearchSpeed;
        for (var step = 0; step < TopSpeedSearchSteps; step++)
        {
            var middle = (low + high) * 0.5f;
            if (GetPowerNeeded(tuning, middle) < tuning.EnginePowerWatts)
                low = middle;
            else
                high = middle;
        }
        return low;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    // Newton metres at the engine to newtons on the road.
    private static float GetWheelRatio(KartTuning tuning, int gear)
    {
        var ratios = tuning.GearRatios;
        return ratios[Math.Clamp(gear, 1, ratios.Length) - 1] * tuning.FinalDrive / tuning.WheelRadiusMeters;
    }
}
