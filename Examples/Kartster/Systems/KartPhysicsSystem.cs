using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;
using Kartster.PowerUps;

namespace Kartster.Systems;

/// <summary>
/// Kartster's kart model on the physics bodies: each fixed step turns every kart's state, its
/// <see cref="KartControls"/> and the surface under it into the step's <see cref="Force"/>
/// (<see cref="KartDynamics.ComputeForces"/>, with the ground's share of the weight as its support: none in the air),
/// plus gravity along the slope under it, which the <see cref="PhysicsSystem"/> after it integrates. One
/// <see cref="KartDynamics"/> per kart, by <see cref="Kart.Index"/>. Runs in the fixed step, before the
/// <see cref="PhysicsSystem"/>.
/// </summary>
[UpdateBefore(typeof(PhysicsSystem))]
public sealed class KartPhysicsSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly KartDynamics[] _dynamics;

    // The physics turns the box by torque / (m (w² + l²) / 12); the kart model by torque / YawInertia, which is that
    // times YawInertiaScale. Dividing the torque by the scale gives the kart model's yaw rate for any scale.
    private readonly float[] _torqueScales;
    private Query _karts;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="tunings">One per kart, by <see cref="Kart.Index"/>.</param>
    public KartPhysicsSystem(params KartTuning[] tunings)
    {
        _dynamics = new KartDynamics[tunings.Length];
        _torqueScales = new float[tunings.Length];
        for (var i = 0; i < tunings.Length; i++)
        {
            _dynamics[i] = new KartDynamics(tunings[i]);
            _torqueScales[i] = 1f / tunings[i].YawInertiaScale;
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int KartCount => _dynamics.Length;

    /// <summary>
    /// Power-up effects laid over each kart's tuning before its step (Kartster's <c>KartEffectTuning</c>, the one seam
    /// through which they reach the kart model); null: none.
    /// </summary>
    public KartEffectTuning Effects { get; set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The kart model of kart <paramref name="index"/>, with its gear, engine rpm and slide state.</summary>
    public KartDynamics GetDynamics(int index) => _dynamics[index];

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _karts = World.Query(Query.With<Kart, KartControls, Force>()
        .With<Position, Rotation, Velocity>().With<AngularVelocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var controls = chunk.GetSpan<KartControls>();
            var forces = chunk.GetSpan<Force>();
            var positions = chunk.GetSpan<Position>();
            var rotations = chunk.GetSpan<Rotation>();
            var velocities = chunk.GetSpan<Velocity>();
            var angularVelocities = chunk.GetSpan<AngularVelocity>();

            for (var i = 0; i < chunk.Count; i++)
            {
                var index = karts[i].Index;
                var body = KartFrame.ToBody(positions[i].Value, rotations[i].Radians, velocities[i].Value,
                    angularVelocities[i].Value);
                var effectTuning = Effects?.Get(index, _dynamics[index], karts[i].Surface);
                var normal = karts[i].Normal;
                var support = karts[i].Airborne ? 0f : normal.Y;
                var kartForces = effectTuning == null
                    ? _dynamics[index].ComputeForces(body, controls[i], karts[i].Surface, deltaSeconds, support)
                    : _dynamics[index].ComputeForces(body, controls[i], effectTuning, deltaSeconds, support);
                // Gravity along the ground, its horizontal part: m·g·sin θ·cos θ downhill (zero in the air).
                var downhill = _dynamics[index].Mass * KartLoadTransfer.Gravity * support;
                forces[i].Add(KartFrame.ToPhysicsForce(kartForces.Force
                    + new System.Numerics.Vector2(normal.X, normal.Z) * downhill));
                forces[i].AddTorque(KartFrame.ToPhysicsTorque(kartForces.Torque) * _torqueScales[index]);
            }
        }
    }
}
