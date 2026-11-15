using System.Numerics;

namespace Kartster.Driving;

/// <summary>Which axles the engine drives. Kartster's <c>KartDriveLayout</c>.</summary>
public enum KartDriveLayout
{
    Rear,
    Front,
    All,
}

/// <summary>
/// How one kart drives: the fields of Kartster's <c>RigidBodyKartTuning</c> that the planar model reads, with the
/// same names, units and defaults, plus the chassis geometry of Kartster's <c>KartChassisGeometry</c>. Defaults are
/// Kartster's balanced kart with no speed class applied; <see cref="KartCatalog"/> has the tuned karts.
/// </summary>
public sealed record KartTuning
{
    //**********************************************************
    //** mass and load transfer
    //**********************************************************

    /// <summary>Kilograms.</summary>
    public float Mass { get; init; } = 200f;

    /// <summary>
    /// Centre of mass relative to the chassis centre, in the kart's own axes (Kartster's: +X right, +Y up, -Z
    /// forward), in metres. Moves the axle arms and the load-transfer height.
    /// </summary>
    public Vector3 CenterOfMassOffset { get; init; } = Vector3.Zero;

    /// <summary>Multiplier on the solid-box yaw inertia.</summary>
    public float YawInertiaScale { get; init; } = 1f;

    public float LongitudinalLoadTransferScale { get; init; } = 0.35f;
    public float LateralLoadTransferScale { get; init; } = 1f;

    /// <summary>Share of an axle's grip lost when all its load is on one wheel, in [0, 1].</summary>
    public float LateralLoadSensitivity { get; init; } = 0.3f;

    //**********************************************************
    //** engine and gearbox
    //**********************************************************

    public float EnginePowerWatts { get; init; } = 32000f;
    public float TorquePeakRpm { get; init; } = 1600f;
    public float IdleRpm { get; init; } = 1000f;
    public float RedlineRpm { get; init; } = 12000f;

    /// <summary>Gear ratios, first gear first. Shared, never written.</summary>
    public float[] GearRatios { get; init; } = [1f];

    public float FinalDrive { get; init; } = 4.19f;
    public float WheelRadiusMeters { get; init; } = 0.2f;
    public KartDriveLayout DriveLayout { get; init; } = KartDriveLayout.Rear;

    //**********************************************************
    //** tyres and brakes
    //**********************************************************

    /// <summary>Lengthways grip of the driven and braking tyres, times axle load.</summary>
    public float TractionFriction { get; init; } = 5f;

    /// <summary>Sideways grip, times axle load.</summary>
    public float TyreFriction { get; init; } = 1.9f;

    public float RearGripScale { get; init; } = 1f;

    /// <summary>Rear grip once the rear slides past 20 degrees, as a share.</summary>
    public float RearSlidingGripScale { get; init; } = 1f;

    /// <summary>Sideways grip a spinning tyre keeps, as a share.</summary>
    public float SpinningSidewaysGripShare { get; init; } = KartTraction.MinimumSidewaysGripShare;

    /// <summary>How far the front wheels turn into a rear slide by themselves, as a share of the slide angle.</summary>
    public float CasterShare { get; init; } = 1f;

    public float BrakeForceNewtons { get; init; } = 5000f;
    public float BrakeFrontShare { get; init; } = 0.7f;

    //**********************************************************
    //** resistance and damping
    //**********************************************************

    /// <summary>Newtons per (kilogram · metre per second), along the kart.</summary>
    public float RollingResistance { get; init; } = 0.1f;

    /// <summary>Newtons per (metre per second)², along the kart.</summary>
    public float AerodynamicDrag { get; init; } = 0.62f;

    /// <summary>Yaw rate removed per second while an axle slides.</summary>
    public float SlideYawDamping { get; init; } = 2f;

    /// <summary>Yaw rate removed per second while an axle slides with the handbrake on.</summary>
    public float HandbrakeYawDamping { get; init; } = 2f;

    /// <summary>Share of the yaw rate removed per second, always, in [0, 1].</summary>
    public float AngularDamping { get; init; } = 0.3f;

    //**********************************************************
    //** steering
    //**********************************************************

    public float MaximumSteerAngleDegrees { get; init; } = 30f;
    public float LowSpeedSteerAngleDegrees { get; init; } = 45f;
    public float HighSpeedSteerScale { get; init; } = 1f;

    //**********************************************************
    //** chassis (Kartster's KartChassisGeometry)
    //**********************************************************

    /// <summary>Collider box width (X), in metres. Also the inertia box.</summary>
    public float ChassisWidthMeters { get; init; } = 1.6f;

    /// <summary>Collider box height (Y), in metres. Half of it is the ride height.</summary>
    public float ChassisHeightMeters { get; init; } = 1f;

    /// <summary>Collider box length (Z), in metres.</summary>
    public float ChassisLengthMeters { get; init; } = 2.8f;

    /// <summary>Chassis centre to front axle, in metres.</summary>
    public float FrontAxleOffsetMeters { get; init; } = 1f;

    /// <summary>Chassis centre to rear axle, in metres.</summary>
    public float RearAxleOffsetMeters { get; init; } = 1f;

    /// <summary>Distance between the left and right wheels, in metres. Kartster defaults it to the box width.</summary>
    public float TrackWidthMeters { get; init; } = 1.6f;

    public float WheelbaseMeters => FrontAxleOffsetMeters + RearAxleOffsetMeters;
    public float RideHeightMeters => ChassisHeightMeters / 2f;
}
