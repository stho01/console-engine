using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using KartViewer.Components;
using KartViewer.Driving;
using Microsoft.Xna.Framework;

namespace KartViewer;

/// <summary>Creates the driven kart as a physics body, in the physics frame (<see cref="KartFrame"/>).</summary>
public static class KartEntity
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="world">Where the kart is created.</param>
    /// <param name="tuning">Gives the mass and the chassis box.</param>
    /// <param name="position">(world X, world Z) of the centre of mass.</param>
    /// <param name="heading">Kart heading: 0 faces -Z, positive is turned left.</param>
    public static Entity Create(World world, KartTuning tuning, Vector2 position, float heading)
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
            Collider.Box(halfSize));
        world.Add(kart, new Force());
        world.Add(kart, new Interpolation());
        world.Add(kart, new RenderOffset());
        world.Add(kart, new Kart());
        world.Add(kart, new KartControls());
        return kart;
    }
}
