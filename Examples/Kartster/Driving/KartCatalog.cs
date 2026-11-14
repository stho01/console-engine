using System.Numerics;

namespace Kartster.Driving;

/// <summary>
/// Tuning for the karts Kartster can drive, from Kartster's <c>KartCatalog</c> (<c>Tune*</c>, <c>Chassis</c> and the
/// <c>Use*Gearbox</c> helpers) with the default speed class, Joyride (<c>SpeedClasses.Apply</c>), applied. Only what
/// the planar model reads: body roll, pitch, wheel lock, downforce, the unicycle and the boost tricks (flying, robot,
/// ghost) are left out, see <c>docs/agent/03-rules.md</c>. <see cref="ForKart"/> finds a kart by its folder name.
/// </summary>
public static class KartCatalog
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float JoyridePowerScale = 1.275f;
    private const float JoyrideGripScale = 1.2f;
    private const float DefaultTrackWidthMeters = 1.6f;
    private const float LongKartLowSpeedSteerAngleDegrees = 30f;
    private const float LongKartSteerAngleDegrees = 20f;
    private const float SixSpeedTorquePeakRpm = 3500f;
    private const float SixSpeedRedlineRpm = 7000f;
    private const float SixSpeedFinalDrive = 9.16f;

    private static readonly float[] SixSpeedGearRatios = [1f, 0.77f, 0.59f, 0.455f, 0.35f, 0.25f];
    private static readonly float[] ThreeSpeedGearRatios = [1f, 0.5f, 0.25f];
    private static readonly float[] FiveSpeedGearRatios = [1f, 0.613f, 0.411f, 0.315f, 0.244f]; // DMC-12
    private static readonly float[] BeetleGearRatios = [1f, 0.542f, 0.332f, 0.234f];
    private static readonly float[] GolfGtiGearRatios = [1f, 0.562f, 0.374f, 0.281f];
    private static readonly float[] Fiat500GearRatios = [1f, 0.559f, 0.351f, 0.236f];
    private static readonly float[] RenaultFiveTurboGearRatios = [1f, 0.613f, 0.411f, 0.315f, 0.259f];
    private static readonly float[] MiniCooperSGearRatios = [1f, 0.599f, 0.424f, 0.3125f];
    private static readonly float[] Lancia037GearRatios = [1f, 0.667f, 0.470f, 0.350f, 0.291f];
    private static readonly float[] FourSpeedGearRatios = [1f, 0.569f, 0.392f, 0.252f]; // 1959 Hydra-Matic

    /// <summary>
    /// The standard go-kart (Kartster's built-in "Kart"). Banana, Hotdog, Kitcar, Lightcycle and Trike have the same
    /// tuning (<c>TuneGoKart</c>) and the default chassis, so <see cref="ForKart"/> gives them this one.
    /// </summary>
    public static readonly KartTuning Kart = Joyride(GoKart());

    /// <summary>A go-kart with a narrow track and a wide lock. Kartster's body roll is not modelled.</summary>
    public static readonly KartTuning Olabil = Joyride(GoKart() with
    {
        MaximumSteerAngleDegrees = 45f,
        TrackWidthMeters = 1.05f,
        // Takes back what the narrow track adds to sideways load transfer, so it grips like a regular kart.
        LateralLoadTransferScale = 1.05f / DefaultTrackWidthMeters,
    });

    /// <summary>A station wagon: heavier, four-wheel drive, six gears. Default chassis.</summary>
    public static readonly KartTuning Wagon = Joyride(SixSpeed() with
    {
        Mass = 300f,
        EnginePowerWatts = 48000f,
        RollingResistance = 0.13f,
        CenterOfMassOffset = new Vector3(0f, -0.1f, 0f),
        DriveLayout = KartDriveLayout.All,
        TractionFriction = 3f,
        TyreFriction = 1.8f,
        BrakeForceNewtons = 9000f,
        HandbrakeYawDamping = 3f,
        LowSpeedSteerAngleDegrees = 38f,
        MaximumSteerAngleDegrees = 25f,
    });

    /// <summary>Oleg, the Lego car: a go-kart on a tiny chassis, with three gears.</summary>
    public static readonly KartTuning Oleg = Joyride(GoKart() with
    {
        TorquePeakRpm = SixSpeedTorquePeakRpm,
        RedlineRpm = SixSpeedRedlineRpm,
        GearRatios = ThreeSpeedGearRatios,
        FinalDrive = SixSpeedFinalDrive,
        LateralLoadTransferScale = 0.64f / DefaultTrackWidthMeters,
        ChassisWidthMeters = 0.64f,
        ChassisHeightMeters = 1.04f,
        ChassisLengthMeters = 1.6f,
        FrontAxleOffsetMeters = 0.56f,
        RearAxleOffsetMeters = 0.56f,
        TrackWidthMeters = 0.64f,
    });

    /// <summary>The Elephant, a truck: Kartster's wheel lock and body roll are not modelled.</summary>
    public static readonly KartTuning Elephant = Joyride(SixSpeed() with
    {
        Mass = 700f,
        CenterOfMassOffset = new Vector3(0f, -0.2f, 0f),
        EnginePowerWatts = 90000f,
        RollingResistance = 0.092f,
        BrakeForceNewtons = 12500f,
        TyreFriction = 2.15f,
        TractionFriction = 2.6f,
        LowSpeedSteerAngleDegrees = LongKartLowSpeedSteerAngleDegrees,
        MaximumSteerAngleDegrees = 23f,
        ChassisWidthMeters = 2f,
        ChassisHeightMeters = 2.4f,
        ChassisLengthMeters = 4.6f,
        FrontAxleOffsetMeters = 1.42f,
        RearAxleOffsetMeters = 1.42f,
        TrackWidthMeters = 1.64f,
    });

    /// <summary>Il Tempo Gigante: an oversized engine, traction under its grip, so it drifts on the throttle.</summary>
    public static readonly KartTuning Itg = Joyride(SixSpeed() with
    {
        TyreFriction = 2f,
        Mass = 450f,
        EnginePowerWatts = 81500f,
        RollingResistance = 0.11f,
        CenterOfMassOffset = new Vector3(0f, -0.15f, 0f),
        TractionFriction = 1.72f,
        BrakeForceNewtons = 10200f,
        HandbrakeYawDamping = 0f,
        HighSpeedSteerScale = 0.5f,
        YawInertiaScale = 0.75f,
        LowSpeedSteerAngleDegrees = LongKartLowSpeedSteerAngleDegrees,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.9f,
        ChassisHeightMeters = 1.3f,
        ChassisLengthMeters = 6f,
        FrontAxleOffsetMeters = 1.8f,
        RearAxleOffsetMeters = 1.8f,
        TrackWidthMeters = 1.5f,
    });

    /// <summary>Halden, a slammed Volvo 245 wagon: grips, stays flat, stops hard.</summary>
    public static readonly KartTuning Halden = Joyride(HaldenTuning());

    /// <summary>DeLorelei, a DeLorean DMC-12: Halden a touch lighter, on five gears. Flying is not modelled.</summary>
    public static readonly KartTuning DeLorelei = Joyride(HaldenTuning() with
    {
        Mass = 365f,
        EnginePowerWatts = 54000f,
        GearRatios = FiveSpeedGearRatios,
        FinalDrive = 9.39f,
        LowSpeedSteerAngleDegrees = 25.7f,
        ChassisWidthMeters = 1.988f,
        ChassisHeightMeters = 1.14f,
        ChassisLengthMeters = 4.267f,
        FrontAxleOffsetMeters = 1.284f,
        RearAxleOffsetMeters = 1.124f,
        TrackWidthMeters = 1.77f,
    });

    /// <summary>Hektor-1, a 1959 Cadillac ambulance: heavy and long. Body roll and the ghost are not modelled.</summary>
    public static readonly KartTuning Hektor = Joyride(HaldenTuning() with
    {
        Mass = 600f,
        EnginePowerWatts = 100300f,
        RollingResistance = 0.151f,
        CenterOfMassOffset = new Vector3(0f, -0.22f, 0f),
        GearRatios = FourSpeedGearRatios,
        FinalDrive = 9.09f,
        TyreFriction = 2.4f,
        TractionFriction = 2.5f,
        LateralLoadTransferScale = 1f,
        BrakeForceNewtons = 14500f,
        LowSpeedSteerAngleDegrees = 29.5f,
        ChassisWidthMeters = 2.04f,
        ChassisHeightMeters = 1.7f,
        ChassisLengthMeters = 6.32f,
        FrontAxleOffsetMeters = 2.24f,
        RearAxleOffsetMeters = 1.72f,
        TrackWidthMeters = 1.55f,
    });

    /// <summary>Whosp, a 1967 VW Beetle: light, rear heavy, long gears. Body roll and the robot are not modelled.</summary>
    public static readonly KartTuning Whosp = Joyride(SixSpeed() with
    {
        Mass = 240f,
        EnginePowerWatts = 42000f,
        RollingResistance = 0.133f,
        CenterOfMassOffset = new Vector3(0f, -0.3f, 0.14f),
        GearRatios = BeetleGearRatios,
        FinalDrive = 9.78f,
        TyreFriction = 2.1f,
        TractionFriction = 2.4f,
        BrakeForceNewtons = 6450f,
        BrakeFrontShare = 0.6f,
        LowSpeedSteerAngleDegrees = 23.6f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.54f,
        ChassisHeightMeters = 1.5f,
        ChassisLengthMeters = 3.84f,
        FrontAxleOffsetMeters = 1.3f,
        RearAxleOffsetMeters = 1.1f,
        TrackWidthMeters = 1.33f,
    });

    /// <summary>Baffi 500, a 1971 Fiat 500 Abarth: kart-light, rear-engined, loose. Body roll is not modelled.</summary>
    public static readonly KartTuning Baffi = Joyride(SixSpeed() with
    {
        Mass = 200f,
        EnginePowerWatts = 34800f,
        RollingResistance = 0.117f,
        CenterOfMassOffset = new Vector3(0f, -0.25f, 0.08f),
        GearRatios = Fiat500GearRatios,
        FinalDrive = 9.70f,
        TyreFriction = 1.95f,
        TractionFriction = 2.4f,
        LateralLoadTransferScale = 1.128f / DefaultTrackWidthMeters,
        BrakeForceNewtons = 5400f,
        BrakeFrontShare = 0.6f,
        LowSpeedSteerAngleDegrees = 23.2f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.36f,
        ChassisHeightMeters = 1.325f,
        ChassisLengthMeters = 2.80f,
        FrontAxleOffsetMeters = 0.985f,
        RearAxleOffsetMeters = 0.855f,
        TrackWidthMeters = 1.128f,
    });

    /// <summary>Pooper S, a 1968 Mini Cooper S: tiny, light, front-wheel drive, corners flat.</summary>
    public static readonly KartTuning Pooper = Joyride(SixSpeed() with
    {
        Mass = 210f,
        EnginePowerWatts = 31500f,
        RollingResistance = 0.093f,
        CenterOfMassOffset = new Vector3(0f, -0.22f, -0.23f),
        DriveLayout = KartDriveLayout.Front,
        GearRatios = MiniCooperSGearRatios,
        FinalDrive = 7.33f,
        TyreFriction = 2.05f,
        TractionFriction = 2.4f,
        LateralLoadTransferScale = 1.19f / DefaultTrackWidthMeters,
        BrakeForceNewtons = 5650f,
        BrakeFrontShare = 0.7f,
        LowSpeedSteerAngleDegrees = 25.1f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.41f,
        ChassisHeightMeters = 1.346f,
        ChassisLengthMeters = 3.054f,
        FrontAxleOffsetMeters = 1.047f,
        RearAxleOffsetMeters = 0.989f,
        TrackWidthMeters = 1.19f,
    });

    /// <summary>Lasagna 037, a 1983 Lancia 037 Rally: mid-engined, rear-wheel drive, drifts on the throttle.</summary>
    public static readonly KartTuning Lasagna = Joyride(SixSpeed() with
    {
        Mass = 280f,
        EnginePowerWatts = 40000f,
        RollingResistance = 0.091f,
        CenterOfMassOffset = new Vector3(0f, -0.15f, 0.11f),
        GearRatios = Lancia037GearRatios,
        FinalDrive = 7.86f,
        TyreFriction = 2.3f,
        TractionFriction = 1.75f,
        BrakeForceNewtons = 7800f,
        BrakeFrontShare = 0.6f,
        LowSpeedSteerAngleDegrees = 24.7f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.85f,
        ChassisHeightMeters = 1.245f,
        ChassisLengthMeters = 3.915f,
        FrontAxleOffsetMeters = 1.1075f,
        RearAxleOffsetMeters = 1.3325f,
        TrackWidthMeters = 1.499f,
    });

    /// <summary>Raleur 5 Turbo, a 1985 Renault 5 Maxi Turbo: the small rally car, quick into a slide and out.</summary>
    public static readonly KartTuning Raleur = Joyride(SixSpeed() with
    {
        Mass = 265f,
        EnginePowerWatts = 38000f,
        RollingResistance = 0.095f,
        CenterOfMassOffset = new Vector3(0f, -0.2f, 0.216f),
        GearRatios = RenaultFiveTurboGearRatios,
        FinalDrive = 8.84f,
        TyreFriction = 2.3f,
        TractionFriction = 1.7f,
        LateralLoadTransferScale = 1.43f / DefaultTrackWidthMeters,
        BrakeForceNewtons = 6900f,
        BrakeFrontShare = 0.6f,
        HandbrakeYawDamping = 7f,
        HighSpeedSteerScale = 0.3f,
        YawInertiaScale = 0.4f,
        LowSpeedSteerAngleDegrees = 25.9f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.752f,
        ChassisHeightMeters = 1.323f,
        ChassisLengthMeters = 3.664f,
        FrontAxleOffsetMeters = 1.242f,
        RearAxleOffsetMeters = 1.188f,
        TrackWidthMeters = 1.43f,
    });

    /// <summary>
    /// McLuring Mp4/4, a 1988 McLaren MP4/4. Not shipped: its grip comes from Kartster's downforce
    /// (<c>DownforceSpeedMetersPerSecond</c> 14 m/s, × 1.275^(1/3) in Joyride), which the port does not model, so
    /// it would corner at its bare 0.84 g at every speed. Kept so the tuning is here when downforce is.
    /// </summary>
    public static readonly KartTuning McLuring = Joyride(SixSpeed() with
    {
        Mass = 220f,
        EnginePowerWatts = 35000f,
        RollingResistance = 0.115f,
        CenterOfMassOffset = new Vector3(0f, -0.2f, 0.3f),
        YawInertiaScale = 0.4f,
        TyreFriction = 0.7f,
        TractionFriction = 0.8f,
        SpinningSidewaysGripShare = 0.05f,
        SlideYawDamping = 0.5f,
        BrakeForceNewtons = 6600f,
        BrakeFrontShare = 0.6f,
        LateralLoadTransferScale = 1.747f / DefaultTrackWidthMeters,
        LowSpeedSteerAngleDegrees = 28f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 2.122f,
        ChassisHeightMeters = 0.95f,
        ChassisLengthMeters = 4.394f,
        FrontAxleOffsetMeters = 1.231f,
        RearAxleOffsetMeters = 1.644f,
        TrackWidthMeters = 1.747f,
    });

    /// <summary>Volkswagner Rolf GTI, a 1976 Golf GTI: a light hot hatch, front-wheel drive.</summary>
    public static readonly KartTuning Rolf = Joyride(SixSpeed() with
    {
        Mass = 260f,
        EnginePowerWatts = 45300f,
        RollingResistance = 0.13f,
        CenterOfMassOffset = new Vector3(0f, -0.2f, 0f),
        DriveLayout = KartDriveLayout.Front,
        GearRatios = GolfGtiGearRatios,
        FinalDrive = 8.15f,
        TyreFriction = 2f,
        TractionFriction = 2.4f,
        LateralLoadTransferScale = 0.8f,
        BrakeForceNewtons = 7000f,
        BrakeFrontShare = 0.7f,
        LowSpeedSteerAngleDegrees = 24.4f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.63f,
        ChassisHeightMeters = 1.395f,
        ChassisLengthMeters = 3.725f,
        FrontAxleOffsetMeters = 1.149f,
        RearAxleOffsetMeters = 1.251f,
        TrackWidthMeters = 1.39f,
    });

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Tuning by kart folder name, any case; <see cref="Kart"/> for the go-kart-tuned ones (banana, hotdog, kitcar,
    /// lightcycle, trike) and any other name. Kartster's unicycle has no entry: it is not shipped.
    /// </summary>
    public static KartTuning ForKart(string name) => name?.ToLowerInvariant() switch
    {
        "olabil" => Olabil,
        "wagon" => Wagon,
        "oleg" => Oleg,
        "elephant" => Elephant,
        "itg" => Itg,
        "halden" => Halden,
        "delorelei" => DeLorelei,
        "hektor" => Hektor,
        "whosp" => Whosp,
        "baffi" => Baffi,
        "pooper" => Pooper,
        "lasagna" => Lasagna,
        "raleur" => Raleur,
        "mcluring" => McLuring,
        "rolf" => Rolf,
        _ => Kart,
    };

    //**********************************************************
    //** internals
    //**********************************************************

    // Brakes mostly at the rear, and a handbrake held back from spinning the light kart round.
    private static KartTuning GoKart() => new() { BrakeFrontShare = 0.4f, HandbrakeYawDamping = 4f };

    // A road engine and Kartster's six-speed gearbox; the other road gearboxes change the ratios and final drive.
    private static KartTuning SixSpeed() => new()
    {
        TorquePeakRpm = SixSpeedTorquePeakRpm,
        RedlineRpm = SixSpeedRedlineRpm,
        GearRatios = SixSpeedGearRatios,
        FinalDrive = SixSpeedFinalDrive,
    };

    // Kartster's TuneHalden, also the start of DeLorelei and Hektor.
    private static KartTuning HaldenTuning() => SixSpeed() with
    {
        Mass = 380f,
        EnginePowerWatts = 60000f,
        RollingResistance = 0.13f,
        CenterOfMassOffset = new Vector3(0f, -0.25f, 0f),
        TyreFriction = 2.1f,
        TractionFriction = 2.6f,
        LateralLoadTransferScale = 0.6f,
        LongitudinalLoadTransferScale = 0.25f,
        BrakeForceNewtons = 11500f,
        BrakeFrontShare = 0.65f,
        LowSpeedSteerAngleDegrees = 28f,
        MaximumSteerAngleDegrees = LongKartSteerAngleDegrees,
        ChassisWidthMeters = 1.71f,
        ChassisHeightMeters = 1.46f,
        ChassisLengthMeters = 4.9f,
        FrontAxleOffsetMeters = 1.53f,
        RearAxleOffsetMeters = 1.11f,
        TrackWidthMeters = 1.38f,
    };

    private static KartTuning Joyride(KartTuning tuning) => tuning with
    {
        EnginePowerWatts = tuning.EnginePowerWatts * JoyridePowerScale,
        TyreFriction = tuning.TyreFriction * JoyrideGripScale,
        TractionFriction = tuning.TractionFriction * JoyrideGripScale,
        BrakeForceNewtons = tuning.BrakeForceNewtons * JoyrideGripScale,
    };
}
