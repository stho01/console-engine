using ConsoleEngine.Ecs;
using ConsoleEngine.Physics;
using Kartster.Bots;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Systems;

namespace Kartster.PowerUps;

/// <summary>
/// The glitch and the earthquake on the steering (Kartster's <c>ApplyInputModifiers</c>), for one fixed step: after
/// the bots and the keys have set each kart's <see cref="KartControls.Steer"/> and before the
/// <see cref="KartPhysicsSystem"/> reads it, the steering becomes <see cref="PowerUpField.Steer"/>'s; the
/// <see cref="PowerUpContactSystem"/> puts the driver's own back, so the keys' and bots' ramps go on from it.
/// </summary>
[UpdateAfter(typeof(BotDriverSystem))]
[UpdateBefore(typeof(KartPhysicsSystem))]
public sealed class PowerUpSteeringSystem(PowerUpField field) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly float[] _driverSteer = new float[field.KartCount];
    private Query _karts;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Puts every kart's own steering back (<see cref="PowerUpContactSystem"/>).</summary>
    public void Restore()
    {
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var controls = chunk.GetSpan<KartControls>();
            for (var i = 0; i < chunk.Count; i++)
                controls[i].Steer = _driverSteer[karts[i].Index];
        }
    }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _karts = World.Query(Query.With<Kart, KartControls>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var controls = chunk.GetSpan<KartControls>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var index = karts[i].Index;
                _driverSteer[index] = controls[i].Steer;
                controls[i].Steer = field.Steer(index, controls[i].Steer);
            }
        }
    }
}

/// <summary>
/// How karts meet each other while an anvil or invulnerability runs (Kartster's <c>KartContactMass</c>): after the
/// <see cref="KartPhysicsSystem"/> has set each kart's <see cref="Force"/> and before the <see cref="PhysicsSystem"/>
/// solves the contacts, a kart's <see cref="RigidBody.InverseMass"/> is divided by <see cref="PowerUpField.ContactMass"/>
/// (4 with an anvil, 1000 invulnerable) and its force and torque multiplied by it, so it drives exactly as before while
/// the solver sees it that much heavier. A wall (no mass) stops it as before. The <see cref="PowerUpSystem"/> puts the
/// real mass back after the step. Also puts the drivers' own steering back (<see cref="PowerUpSteeringSystem"/>).
/// </summary>
/// <remarks>
/// Kartster also gives a contact with an anvil kart a restitution of at least 0.7; the port's pairs take the larger
/// of the two colliders' restitution, which would make the anvil kart bounce off walls too, so that is left out.
/// </remarks>
[UpdateAfter(typeof(KartPhysicsSystem))]
[UpdateBefore(typeof(PhysicsSystem))]
public sealed class PowerUpContactSystem(PowerUpField field, PowerUpSteeringSystem steering = null) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _karts;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _karts = World.Query(Query.With<Kart, RigidBody, Force>());

    protected override void OnUpdate(float deltaSeconds)
    {
        steering?.Restore();
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var bodies = chunk.GetSpan<RigidBody>();
            var forces = chunk.GetSpan<Force>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var mass = field.ContactMass(karts[i].Index);
                if (mass == 1f)
                    continue;

                bodies[i].InverseMass /= mass;
                forces[i].Value *= mass;
                forces[i].Torque *= mass;
            }
        }
    }
}
