using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using KartViewer.Components;
using KartViewer.Driving;

namespace KartViewer.Systems;

/// <summary>
/// Keeps the kart inside the ground square and standing on the road or the grass, and notes which one it is on for
/// the next step's grip. Runs in the fixed step, after the <see cref="PhysicsSystem"/>.
/// </summary>
[UpdateAfter(typeof(PhysicsSystem))]
public sealed class KartGroundSystem(Entity kart, Track track) : EntitySystem
{
    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        ref var position = ref World.Get<Position>(kart).Value;
        ref var velocity = ref World.Get<Velocity>(kart).Value;
        var center = track.GroundCenter;
        var edge = track.GroundHalfSize - 1;

        // At the edge the kart stops going outwards, so it slides along it.
        var x = Math.Clamp(position.X, center.X - edge, center.X + edge);
        var z = Math.Clamp(position.Y, center.Z - edge, center.Z + edge);
        if (x != position.X) velocity.X = 0f;
        if (z != position.Y) velocity.Y = 0f;
        position = new Microsoft.Xna.Framework.Vector2(x, z);

        ref var state = ref World.Get<Kart>(kart);
        var onRoad = track.TryGetRoadHeight(new System.Numerics.Vector3(x, 0f, z), out state.Height);
        state.Surface = onRoad ? Surface.Asphalt : Surface.Grass;
    }
}
