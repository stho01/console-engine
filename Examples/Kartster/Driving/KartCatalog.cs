using System.Numerics;

namespace Kartster.Driving;

/// <summary>
/// Tuning for the karts Kartster can drive, from Kartster's <c>KartCatalog</c> (<c>TuneGoKart</c>,
/// <c>TuneOlabil</c>, <c>TuneWagon</c>) with the default speed class, Joyride (<c>SpeedClasses.Apply</c>), applied.
/// Only what the planar model reads: body roll, rev counter and the like are left out.
/// </summary>
public static class KartCatalog
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float JoyridePowerScale = 1.275f;
    private const float JoyrideGripScale = 1.2f;

    /// <summary>The standard go-kart (Kartster's built-in "Kart").</summary>
    public static readonly KartTuning Kart = Joyride(GoKart());

    /// <summary>A go-kart with a narrow track and a wide lock. Kartster's body roll is not modelled.</summary>
    public static readonly KartTuning Olabil = Joyride(GoKart() with
    {
        MaximumSteerAngleDegrees = 45f,
        TrackWidthMeters = 1.05f,
        // Takes back what the narrow track adds to sideways load transfer, so it grips like a regular kart.
        LateralLoadTransferScale = 1.05f / 1.6f,
    });

    /// <summary>A station wagon: heavier, four-wheel drive, six gears. Default chassis.</summary>
    public static readonly KartTuning Wagon = Joyride(new KartTuning
    {
        Mass = 300f,
        EnginePowerWatts = 48000f,
        RollingResistance = 0.13f,
        CenterOfMassOffset = new Vector3(0f, -0.1f, 0f),
        DriveLayout = KartDriveLayout.All,
        TorquePeakRpm = 3500f,
        RedlineRpm = 7000f,
        GearRatios = [1f, 0.77f, 0.59f, 0.455f, 0.35f, 0.25f],
        FinalDrive = 9.16f,
        TractionFriction = 3f,
        TyreFriction = 1.8f,
        BrakeForceNewtons = 9000f,
        HandbrakeYawDamping = 3f,
        LowSpeedSteerAngleDegrees = 38f,
        MaximumSteerAngleDegrees = 25f,
    });

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Tuning by kart folder name ("Kart", "olabil", "wagon"), any case; <see cref="Kart"/> otherwise.</summary>
    public static KartTuning ForKart(string name) => name?.ToLowerInvariant() switch
    {
        "olabil" => Olabil,
        "wagon" => Wagon,
        _ => Kart,
    };

    //**********************************************************
    //** internals
    //**********************************************************

    // Brakes mostly at the rear, and a handbrake held back from spinning the light kart round.
    private static KartTuning GoKart() => new() { BrakeFrontShare = 0.4f, HandbrakeYawDamping = 4f };

    private static KartTuning Joyride(KartTuning tuning) => tuning with
    {
        EnginePowerWatts = tuning.EnginePowerWatts * JoyridePowerScale,
        TyreFriction = tuning.TyreFriction * JoyrideGripScale,
        TractionFriction = tuning.TractionFriction * JoyrideGripScale,
        BrakeForceNewtons = tuning.BrakeForceNewtons * JoyrideGripScale,
    };
}
