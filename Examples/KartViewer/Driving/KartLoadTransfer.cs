using System;

namespace KartViewer.Driving;

/// <summary>Newtons on each wheel. Always adds up to the kart's weight.</summary>
public readonly record struct KartWheelLoads(float FrontLeft, float FrontRight, float RearLeft, float RearRight)
{
    public float Front => FrontLeft + FrontRight;
    public float Rear => RearLeft + RearRight;
}

/// <summary>
/// Weight on each wheel from the kart's acceleration, after Kartster's <c>KartLoadTransfer</c>: load moves opposite
/// to the acceleration, by centre-of-mass height over wheelbase (lengthways) or track width (sideways), and is
/// clamped so it is moved, never made.
/// </summary>
public static class KartLoadTransfer
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float Gravity = 9.81f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="forwardAcceleration">Along the kart, metres per second².</param>
    /// <param name="rightAcceleration">To the kart's right, metres per second².</param>
    public static KartWheelLoads Calculate(KartTuning tuning, float forwardAcceleration, float rightAcceleration)
    {
        var weight = tuning.Mass * Gravity;
        var wheelbase = tuning.WheelbaseMeters;
        var trackWidth = tuning.TrackWidthMeters;
        var centerForward = -tuning.CenterOfMassOffset.Z;
        var centerRight = tuning.CenterOfMassOffset.X;
        var centerHeight = MathF.Max(tuning.RideHeightMeters + tuning.CenterOfMassOffset.Y, 0f);

        var frontLoad = weight * ((tuning.RearAxleOffsetMeters + centerForward) / wheelbase);
        var longitudinalTransfer = tuning.Mass * forwardAcceleration * centerHeight / wheelbase
            * MathF.Max(tuning.LongitudinalLoadTransferScale, 0f);
        frontLoad = Math.Clamp(frontLoad - longitudinalTransfer, 0f, weight);
        var rearLoad = weight - frontLoad;

        var lateralScale = MathF.Max(tuning.LateralLoadTransferScale, 0f);
        var frontLeft = LeftShare(frontLoad, rightAcceleration, centerRight, centerHeight, trackWidth, lateralScale);
        var rearLeft = LeftShare(rearLoad, rightAcceleration, centerRight, centerHeight, trackWidth, lateralScale);
        return new KartWheelLoads(frontLeft, frontLoad - frontLeft, rearLeft, rearLoad - rearLeft);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float LeftShare(float axleLoad, float rightAcceleration, float centerRight, float centerHeight,
        float trackWidth, float transferScale)
    {
        var rightShare = Math.Clamp((trackWidth / 2f + centerRight) / trackWidth, 0f, 1f);
        var lateralTransfer = axleLoad / Gravity * rightAcceleration * centerHeight / trackWidth * transferScale;
        return Math.Clamp(axleLoad * (1f - rightShare) + lateralTransfer, 0f, axleLoad);
    }
}
