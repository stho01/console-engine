using System;
using System.Numerics;

namespace KartViewer.Driving;

/// <summary>
/// Kartster's arcade kart model (<c>ArcadeKartPhysics.Update</c>) on flat ground, always grounded, as forces: each
/// step <see cref="ComputeForces"/> turns the body's state and the controls into one net force and yaw torque,
/// which an integrator holds constant over the step (<see cref="KartIntegrator"/>, or a rigid-body engine).
/// </summary>
/// <remarks>
/// <para>
/// Frame (see <see cref="KartBody"/>): x = world X, y = world Z, forward at heading 0 is (0, -1), heading and yaw
/// rate are positive turning left. A yaw torque from a force F at arm r is r.Y·F.X - r.X·F.Y, the velocity of a
/// point is v + ω·(r.Y, -r.X): Kartster's 3D cross products with +Y up.
/// </para>
/// <para>
/// The tyres cancel each axle's sideways speed within the step, capped by friction; Kartster's velocity
/// multiplications (angular damping) are turned into the torque that gives the same yaw rate after the step. Every
/// force is computed from the state at the start of the step, as in Kartster, so with semi-implicit Euler the step
/// is Kartster's.
/// </para>
/// <para>
/// Kept state: gear and shift timer, wheel speeds (wheel spin), and last step's velocity, whose change is the
/// measured acceleration that drives load transfer. Call <see cref="ComputeForces"/> exactly once per step.
/// </para>
/// </remarks>
public sealed class KartDynamics
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float StationarySpeed = 0.1f;
    private const float PowerBandSpeed = 8f;
    public const float MaximumReverseSpeed = 30f / 3.6f;
    private const float SteerFadeStartSpeed = 5f;
    private const float SteerFadeEndSpeed = 15f;
    private const float HighSpeedSteerFadeStart = 15f;
    private const float HighSpeedSteerFadeEnd = 30f;
    private const float CasterFadeStartSpeed = 3f;
    private const float CasterFadeEndSpeed = 6f;
    private const float MaximumCasterDegrees = 45f;
    private const float SlidingGripStartDegrees = 12f;
    private const float SlidingGripFullDegrees = 20f;
    private const float HandbrakeRevs = 0.6f;

    /// <summary>Axle slide speed, metres per second, above which the kart counts as sliding.</summary>
    public const float SlidingThreshold = 0.1f;

    private readonly KartTuning[] _surfaceTunings;
    private float _shiftSecondsLeft;
    private float _frontWheelSpeed;
    private float _rearWheelSpeed;
    private Vector2 _previousVelocity;
    private bool _hasPreviousVelocity;
    private Vector2 _force;
    private float _torque;

    //**********************************************************
    //** ctor
    //**********************************************************

    public KartDynamics(KartTuning tuning)
    {
        Tuning = tuning;
        Mass = MathF.Max(tuning.Mass, 0.001f);
        var width = tuning.ChassisWidthMeters;
        var length = tuning.ChassisLengthMeters;
        YawInertia = Mass * tuning.YawInertiaScale * (width * width + length * length) / 12f;
        TopSpeed = KartEngine.GetTopSpeed(tuning);

        _surfaceTunings = new KartTuning[Enum.GetValues<Surface>().Length];
        for (var i = 0; i < _surfaceTunings.Length; i++)
            _surfaceTunings[i] = ForSurface(tuning, (Surface)i, TopSpeed);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public KartTuning Tuning { get; }

    /// <summary>Kilograms.</summary>
    public float Mass { get; }

    /// <summary>Kilogram metres²: a solid box of the chassis, times <see cref="KartTuning.YawInertiaScale"/>.</summary>
    public float YawInertia { get; }

    /// <summary>Where engine power and resistance balance on asphalt, metres per second.</summary>
    public float TopSpeed { get; }

    /// <summary>One-based.</summary>
    public int Gear { get; private set; } = 1;

    public float EngineRpm { get; private set; }

    /// <summary>Whether an axle slid or spun more than <see cref="SlidingThreshold"/> in the last step.</summary>
    public bool IsSliding { get; private set; }

    /// <summary>How fast the front axle slid sideways (or spun) last step, m/s. Kartster's <c>KartAxleSlip</c>.</summary>
    public float FrontSlip { get; private set; }

    /// <summary>As <see cref="FrontSlip"/>, for the rear axle.</summary>
    public float RearSlip { get; private set; }

    /// <summary>Wheel loads used in the last step, newtons.</summary>
    public KartWheelLoads WheelLoads { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The tuning a surface turns this kart's into: grip, rolling drag and the governed engine.</summary>
    public KartTuning GetTuning(Surface surface) => _surfaceTunings[(int)surface];

    public KartForces ComputeForces(in KartBody body, in KartControls controls, Surface surface, float dt)
    {
        if (dt <= 0f)
            return default;

        var tuning = _surfaceTunings[(int)surface];
        var throttle = Math.Clamp(controls.Throttle, 0f, 1f) - Math.Clamp(controls.Brake, 0f, 1f);
        var steering = Math.Clamp(controls.Steer, -1f, 1f);
        var handbrake = controls.Handbrake;
        var velocity = body.Velocity;
        var yawRate = body.AngularVelocity;
        var forward = body.Forward;
        var right = body.Right;
        _force = Vector2.Zero;
        _torque = 0f;

        // Load transfer goes by the acceleration measured over the last step.
        var acceleration = _hasPreviousVelocity ? (velocity - _previousVelocity) / dt : Vector2.Zero;
        _previousVelocity = velocity;
        _hasPreviousVelocity = true;
        var loads = KartLoadTransfer.Calculate(tuning, Vector2.Dot(acceleration, forward),
            Vector2.Dot(acceleration, right));
        WheelLoads = loads;

        var forwardSpeed = Vector2.Dot(velocity, forward);
        // The gearbox keeps to the road while the handbrake has the wheels locked.
        var drivenWheelSpeed = handbrake ? forwardSpeed : GetDrivenWheelSpeed(tuning.DriveLayout);
        UpdateGearbox(tuning, drivenWheelSpeed, drivenWheelSpeed - forwardSpeed > KartTraction.SpinSpeed, dt);
        if (handbrake && throttle > 0f)
            EngineRpm = tuning.IdleRpm + throttle * HandbrakeRevs * (tuning.RedlineRpm - tuning.IdleRpm);

        var centerOfMass = forward * -tuning.CenterOfMassOffset.Z + right * tuning.CenterOfMassOffset.X;
        var frontArm = forward * tuning.FrontAxleOffsetMeters - centerOfMass;
        var rearArm = -forward * tuning.RearAxleOffsetMeters - centerOfMass;

        var steerAngle = float.DegreesToRadians(GetSteerAngleDegrees(tuning, forwardSpeed)) * steering
            + GetCasterRadians(tuning, velocity, yawRate, forward, right, rearArm);
        var wheelHeading = body.Heading - steerAngle; // positive steer is to the right
        var frontForward = new Vector2(-MathF.Sin(wheelHeading), -MathF.Cos(wheelHeading));
        var frontRight = new Vector2(-frontForward.Y, frontForward.X);

        var isBraking = MathF.Abs(forwardSpeed) > StationarySpeed && MathF.Sign(throttle) != MathF.Sign(forwardSpeed);
        if (!isBraking && !handbrake)
            ApplyReverse(tuning, throttle, forward, forwardSpeed, dt);
        var traction = ApplyWheels(tuning, isBraking ? 0f : MathF.Max(throttle, 0f), isBraking ? MathF.Abs(throttle) : 0f,
            steering, handbrake, velocity, yawRate, forward, frontForward, frontArm, rearArm, drivenWheelSpeed,
            loads, dt);
        ApplyResistance(tuning, forward, forwardSpeed, dt);
        var (frontSlip, rearSlip) = ApplySteering(tuning, traction, velocity, yawRate, forward, right, frontRight,
            frontArm, rearArm, loads, dt);

        FrontSlip = frontSlip;
        RearSlip = rearSlip;
        IsSliding = frontSlip > SlidingThreshold || rearSlip > SlidingThreshold;
        if (IsSliding)
        {
            // Settles a slide the saturated tyres cannot: removes yaw at a rate, never more than all of it.
            var rate = MathF.Min(MathF.Max(handbrake ? tuning.HandbrakeYawDamping : tuning.SlideYawDamping, 0f), 1f / dt);
            _torque -= yawRate * rate * YawInertia;
        }

        // Kartster multiplies the yaw rate by this before integrating; as a torque it removes the same.
        var keep = MathF.Max(0f, 1f - Math.Clamp(tuning.AngularDamping, 0f, 1f) * dt);
        _torque += yawRate * (keep - 1f) * YawInertia / dt;

        return new KartForces(_force, _torque);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private readonly record struct AxleTraction(float FrontGripShare, float RearGripShare, float FrontSpin,
        float RearSpin);

    private static KartTuning ForSurface(KartTuning tuning, Surface surface, float topSpeed)
    {
        var grip = SurfaceHandling.GetGripMultiplier(surface);
        var onSurface = tuning with
        {
            TyreFriction = tuning.TyreFriction * grip,
            TractionFriction = tuning.TractionFriction * grip,
            RollingResistance = tuning.RollingResistance * SurfaceHandling.GetRollingDragMultiplier(surface),
        };
        var fraction = SurfaceHandling.GetTopSpeedFraction(surface);
        if (fraction >= 1f)
            return onSurface;

        // A governor, as Kartster's KartEffectTuning.ApplySurfaceSpeedLimit: just the power for that share of the
        // kart's own top speed against this surface's drag.
        var power = KartEngine.GetPowerNeeded(onSurface, topSpeed * fraction);
        return onSurface with { EnginePowerWatts = MathF.Min(onSurface.EnginePowerWatts, power) };
    }

    private float GetDrivenWheelSpeed(KartDriveLayout layout) => layout switch
    {
        KartDriveLayout.Front => _frontWheelSpeed,
        KartDriveLayout.All => (_frontWheelSpeed + _rearWheelSpeed) / 2f,
        _ => _rearWheelSpeed,
    };

    // Kartster's KartGearbox as an automatic (no driver requests).
    private void UpdateGearbox(KartTuning tuning, float wheelSpeed, bool isSpinning, float dt)
    {
        var gear = Math.Clamp(Gear, 1, tuning.GearRatios.Length);
        _shiftSecondsLeft = MathF.Max(_shiftSecondsLeft - dt, 0f);
        if (wheelSpeed <= 0f)
        {
            gear = 1;
        }
        else if (_shiftSecondsLeft <= 0f && !isSpinning)
        {
            var next = KartEngine.SelectGear(tuning, gear, wheelSpeed);
            if (next != gear)
            {
                gear = next;
                _shiftSecondsLeft = KartEngine.ShiftSeconds;
            }
        }
        Gear = gear;
        EngineRpm = KartEngine.GetRpm(tuning, gear, wheelSpeed);
    }

    private static float GetSteerAngleDegrees(KartTuning tuning, float forwardSpeed)
    {
        var speed = MathF.Abs(forwardSpeed);
        var fade = Math.Clamp((speed - SteerFadeStartSpeed) / (SteerFadeEndSpeed - SteerFadeStartSpeed), 0f, 1f);
        var highSpeedFade = Math.Clamp(
            (speed - HighSpeedSteerFadeStart) / (HighSpeedSteerFadeEnd - HighSpeedSteerFadeStart), 0f, 1f);
        var steerScale = float.Lerp(1f, tuning.HighSpeedSteerScale, highSpeedFade);
        return float.Lerp(tuning.LowSpeedSteerAngleDegrees, tuning.MaximumSteerAngleDegrees * steerScale, fade);
    }

    // The front wheels turn into a rear slide by themselves, positive to the right like steering.
    private static float GetCasterRadians(KartTuning tuning, Vector2 velocity, float yawRate, Vector2 forward,
        Vector2 right, Vector2 rearArm)
    {
        var rearVelocity = PointVelocity(velocity, yawRate, rearArm);
        var forwardSpeed = Vector2.Dot(rearVelocity, forward);
        var fade = Math.Clamp(
            (forwardSpeed - CasterFadeStartSpeed) / (CasterFadeEndSpeed - CasterFadeStartSpeed), 0f, 1f);
        if (fade <= 0f)
            return 0f;

        var slide = MathF.Atan2(Vector2.Dot(rearVelocity, right), forwardSpeed);
        var maximum = float.DegreesToRadians(MaximumCasterDegrees);
        return Math.Clamp(tuning.CasterShare * slide, -maximum, maximum) * fade;
    }

    // Asks for the force that puts the kart at the reverse limit this step, capped by the launch force.
    private void ApplyReverse(KartTuning tuning, float throttle, Vector2 forward, float forwardSpeed, float dt)
    {
        if (throttle >= 0f)
            return;

        var limit = -throttle * MathF.Max(tuning.EnginePowerWatts, 0f) / PowerBandSpeed;
        var force = Math.Clamp((MaximumReverseSpeed + forwardSpeed) * Mass / dt, -limit, limit);
        _force -= forward * force;
    }

    private AxleTraction ApplyWheels(KartTuning tuning, float throttle, float brake, float steering, bool handbrake,
        Vector2 velocity, float yawRate, Vector2 forward, Vector2 frontForward, Vector2 frontArm, Vector2 rearArm,
        float drivenWheelSpeed, KartWheelLoads loads, float dt)
    {
        var frontRoadSpeed = Vector2.Dot(PointVelocity(velocity, yawRate, frontArm), frontForward);
        var rearRoadSpeed = Vector2.Dot(PointVelocity(velocity, yawRate, rearArm), forward);

        var engineForce = _shiftSecondsLeft > 0f ? 0f : throttle * KartEngine.GetDriveForce(tuning, Gear, drivenWheelSpeed);
        if (throttle <= 0f && drivenWheelSpeed > 0f)
            engineForce = -KartEngine.GetEngineBrakeForce(tuning, Gear, drivenWheelSpeed);
        if (handbrake)
            engineForce *= KartTraction.HandbrakeEngineShare;

        var traction = MathF.Max(tuning.TractionFriction, 0f);
        var drivesFront = tuning.DriveLayout != KartDriveLayout.Rear;
        var drivesRear = tuning.DriveLayout != KartDriveLayout.Front;
        var axleForce = drivesFront && drivesRear ? engineForce / 2f : engineForce;
        var brakeForce = brake * MathF.Max(tuning.BrakeForceNewtons, 0f);
        var frontBrakeShare = Math.Clamp(tuning.BrakeFrontShare, 0f, 1f);
        var spinningShare = tuning.SpinningSidewaysGripShare;

        var front = StepAxle(drivesFront ? axleForce : 0f, brakeForce * frontBrakeShare, drivesFront,
            _frontWheelSpeed, frontRoadSpeed, traction * loads.Front, steering, spinningShare, dt);
        var rear = StepAxle(drivesRear ? axleForce : 0f, brakeForce * (1f - frontBrakeShare), drivesRear,
            _rearWheelSpeed, rearRoadSpeed, traction * loads.Rear, steering, spinningShare, dt);

        // A gear too low for the road holds the driven wheels at its top speed, and they skid.
        var slidingFriction = MathF.Max(tuning.TyreFriction, 0f) * KartTraction.LockedGripScale;
        var gearTopSpeed = KartEngine.GetTopSpeedInGear(tuning, Gear);
        if (drivesFront && frontRoadSpeed > gearTopSpeed * KartTraction.OverRevShare)
            front = KartTraction.Lock(gearTopSpeed, frontRoadSpeed, slidingFriction * loads.Front, Mass, dt);
        if (drivesRear && rearRoadSpeed > gearTopSpeed * KartTraction.OverRevShare)
            rear = KartTraction.Lock(gearTopSpeed, rearRoadSpeed, slidingFriction * loads.Rear, Mass, dt);
        if (handbrake)
            rear = KartTraction.Lock(0f, rearRoadSpeed, slidingFriction * loads.Rear, Mass, dt);

        AddForceAtArm(frontForward * front.Force, frontArm);
        AddForceAtArm(forward * rear.Force, rearArm);
        _frontWheelSpeed = front.WheelSpeed;
        _rearWheelSpeed = rear.WheelSpeed;

        return new AxleTraction(front.SidewaysGripShare, rear.SidewaysGripShare,
            MathF.Abs(front.WheelSpeed - frontRoadSpeed), MathF.Abs(rear.WheelSpeed - rearRoadSpeed));
    }

    private static KartTraction.AxleResult StepAxle(float driveForce, float brakeForce, bool isDriven, float wheelSpeed,
        float roadSpeed, float maximumForce, float steering, float spinningShare, float dt)
    {
        if (brakeForce > 0f)
            return KartTraction.Brake(brakeForce, roadSpeed, maximumForce, steering);
        return isDriven
            ? KartTraction.Step(driveForce, wheelSpeed, roadSpeed, maximumForce, dt, spinningShare)
            : new KartTraction.AxleResult(0f, roadSpeed, 1f);
    }

    // Rolling resistance and drag along the kart; the tyres own the sideways speed.
    private void ApplyResistance(KartTuning tuning, Vector2 forward, float forwardSpeed, float dt)
    {
        var resistance = MathF.Min(MathF.Max(tuning.RollingResistance, 0f), 1f / dt);
        var rolling = -forwardSpeed * resistance * Mass;
        var drag = -MathF.Sign(forwardSpeed) * MathF.Max(tuning.AerodynamicDrag, 0f) * forwardSpeed * forwardSpeed;
        var stopping = MathF.Abs(forwardSpeed) * Mass / dt; // never reverses the kart within a step
        _force += forward * Math.Clamp(rolling + drag, -stopping, stopping);
    }

    private (float FrontSlip, float RearSlip) ApplySteering(KartTuning tuning, AxleTraction traction, Vector2 velocity,
        float yawRate, Vector2 forward, Vector2 right, Vector2 frontRight, Vector2 frontArm, Vector2 rearArm,
        KartWheelLoads loads, float dt)
    {
        var frontVelocity = PointVelocity(velocity, yawRate, frontArm);
        var rearVelocity = PointVelocity(velocity, yawRate, rearArm);
        var frontLateralSpeed = Vector2.Dot(frontVelocity, frontRight);
        var rearLateralSpeed = Vector2.Dot(rearVelocity, right);

        var frontFriction = traction.FrontGripShare * AxleFriction(tuning, loads.Front, loads.FrontLeft, loads.FrontRight);
        // Past its slip angle the rear grips less, which keeps a drift going.
        var rearSlipDegrees = float.RadiansToDegrees(MathF.Atan2(MathF.Abs(rearLateralSpeed),
            MathF.Max(MathF.Abs(Vector2.Dot(rearVelocity, forward)), 1f)));
        var slidingShare = Math.Clamp(
            (rearSlipDegrees - SlidingGripStartDegrees) / (SlidingGripFullDegrees - SlidingGripStartDegrees), 0f, 1f);
        var slidingGrip = float.Lerp(1f, MathF.Max(tuning.RearSlidingGripScale, 0f), slidingShare);
        var rearFriction = traction.RearGripShare * MathF.Max(tuning.RearGripScale, 0f) * slidingGrip
            * AxleFriction(tuning, loads.Rear, loads.RearLeft, loads.RearRight);

        var frontSlip = ApplyTyre(frontLateralSpeed, frontArm, frontRight, frontFriction * loads.Front, dt);
        var rearSlip = ApplyTyre(rearLateralSpeed, rearArm, right, rearFriction * loads.Rear, dt);
        return (MathF.Max(frontSlip, traction.FrontSpin), MathF.Max(rearSlip, traction.RearSpin));
    }

    /// <summary>
    /// Kartster's whole tyre model: the force that cancels the axle's sideways speed this step, through the effective
    /// mass at the axle, capped by friction. Returns the sideways speed left over, which is the slide.
    /// </summary>
    private float ApplyTyre(float lateralSpeed, Vector2 arm, Vector2 slipDirection, float maximumForce, float dt)
    {
        if (maximumForce <= 0f)
            return MathF.Abs(lateralSpeed);

        var armCross = Cross(arm, slipDirection);
        var effectiveMass = 1f / (1f / Mass + armCross * armCross / YawInertia);
        var force = Math.Clamp(-lateralSpeed * effectiveMass / dt, -maximumForce, maximumForce);
        AddForceAtArm(slipDirection * force, arm);
        return MathF.Abs(lateralSpeed + force * dt / effectiveMass);
    }

    // Friction an axle offers after the penalty for carrying its load unevenly.
    private static float AxleFriction(KartTuning tuning, float axleLoad, float leftLoad, float rightLoad)
    {
        var imbalance = axleLoad > 0f ? MathF.Abs(leftLoad - rightLoad) / axleLoad : 0f;
        var evenness = 1f - Math.Clamp(tuning.LateralLoadSensitivity, 0f, 1f) * imbalance;
        return MathF.Max(tuning.TyreFriction, 0f) * evenness;
    }

    private void AddForceAtArm(Vector2 force, Vector2 arm)
    {
        _force += force;
        _torque += Cross(arm, force);
    }

    // Yaw torque (positive left) of a force at an arm: the Y of Kartster's 3D cross product, with y = world Z.
    private static float Cross(Vector2 arm, Vector2 force) => arm.Y * force.X - arm.X * force.Y;

    private static Vector2 PointVelocity(Vector2 velocity, float yawRate, Vector2 arm) =>
        velocity + new Vector2(yawRate * arm.Y, -yawRate * arm.X);
}
