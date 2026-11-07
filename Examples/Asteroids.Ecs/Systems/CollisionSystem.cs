using ConsoleEngine.Ecs;
using ConsoleEngine.Physics;

namespace Asteroids.Ecs.Systems;

/// <summary>
/// A bullet that touches an asteroid destroys both. Bullets are sensors, so every hit is a sensor begin event from
/// the engine's <see cref="PhysicsSystem"/>. Runs after it in the fixed-step group.
/// </summary>
public sealed class CollisionSystem(IPhysicsEvents physics) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        var hits = physics.SensorBeginEvents;
        for (var i = 0; i < hits.Length; i++)
        {
            // Two bullets can hit the same asteroid in one step, and one bullet two asteroids. Playback skips the
            // second destroy of an entity.
            Commands.Destroy(hits[i].Sensor);
            Commands.Destroy(hits[i].Visitor);
        }
    }
}
