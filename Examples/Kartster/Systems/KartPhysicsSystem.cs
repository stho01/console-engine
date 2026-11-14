using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;

namespace Kartster.Systems;

/// <summary>
/// Kartster's kart model on the physics body: each fixed step turns the kart's state, its <see cref="KartControls"/>
/// and the surface under it into the step's <see cref="Force"/> (<see cref="KartDynamics.ComputeForces"/>), which the
/// <see cref="PhysicsSystem"/> after it integrates. Runs in the fixed step, before the <see cref="PhysicsSystem"/>.
/// </summary>
[UpdateBefore(typeof(PhysicsSystem))]
public sealed class KartPhysicsSystem(Entity kart, KartTuning tuning) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    // The physics turns the box by torque / (m (w² + l²) / 12); the kart model by torque / YawInertia, which is that
    // times YawInertiaScale. Dividing the torque by the scale gives the kart model's yaw rate for any scale.
    private readonly float _torqueScale = 1f / tuning.YawInertiaScale;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The kart model, with its gear, engine rpm and slide state. One per kart: it keeps state per step.</summary>
    public KartDynamics Dynamics { get; } = new(tuning);

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        var body = KartFrame.ToBody(World.Get<Position>(kart).Value, World.Get<Rotation>(kart).Radians,
            World.Get<Velocity>(kart).Value, World.Get<AngularVelocity>(kart).Value);
        var forces = Dynamics.ComputeForces(body, World.Get<KartControls>(kart), World.Get<Kart>(kart).Surface,
            deltaSeconds);

        ref var force = ref World.Get<Force>(kart);
        force.Add(KartFrame.ToPhysicsForce(forces.Force));
        force.AddTorque(KartFrame.ToPhysicsTorque(forces.Torque) * _torqueScale);
    }
}
