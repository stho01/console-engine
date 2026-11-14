using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;
using Microsoft.Xna.Framework;

namespace Kartster;

/// <summary>Creates a kart as a physics body, in the physics frame (<see cref="KartFrame"/>).</summary>
public static class KartEntity
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's kart contact bounce (<c>RigidBodyKartTuning.Restitution</c>).</summary>
    public const float Restitution = 0.1f;

    /// <summary>Kartster's kart contact friction (<c>RigidBodyKartTuning.Friction</c>).</summary>
    public const float Friction = 0.3f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="world">Where the kart is created.</param>
    /// <param name="tuning">Gives the mass and the chassis box.</param>
    /// <param name="position">(world X, world Z) of the centre of mass.</param>
    /// <param name="heading">Kart heading: 0 faces -Z, positive is turned left.</param>
    /// <param name="index">The kart's <see cref="Kart.Index"/>.</param>
    public static Entity Create(World world, KartTuning tuning, Vector2 position, float heading, int index = 0)
    {
        // A uniform box has I = m (hx² + hy²) / 3 = m (w² + l²) / 12, which is KartDynamics.YawInertia when
        // YawInertiaScale is 1. KartPhysicsSystem scales the torque for any other value.
        var halfSize = new Vector2(tuning.ChassisWidthMeters / 2f, tuning.ChassisLengthMeters / 2f);
        var kart = world.Create(
            new Position(position),
            new Rotation(KartFrame.ToRadians(heading)),
            new Velocity(Vector2.Zero),
            new AngularVelocity(0f),
            RigidBody.Dynamic(tuning.Mass, gravityScale: 0f),
            Collider.Box(halfSize) with { Restitution = Restitution, Friction = Friction });
        world.Add(kart, new Force());
        world.Add(kart, new Interpolation());
        world.Add(kart, new RenderOffset());
        world.Add(kart, new Kart { Index = index });
        world.Add(kart, new KartControls());
        return kart;
    }
}
