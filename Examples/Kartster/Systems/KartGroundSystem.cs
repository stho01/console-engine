using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Kartster.Components;
using Kartster.Driving;

namespace Kartster.Systems;

/// <summary>
/// Keeps every kart inside the ground square and standing on the road or the grass, and notes which one it is on for
/// the next step's grip. Runs in the fixed step, after the <see cref="PhysicsSystem"/>.
/// </summary>
[UpdateAfter(typeof(PhysicsSystem))]
public sealed class KartGroundSystem(Track track) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _karts;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _karts = World.Query(Query.With<Kart, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var center = track.GroundCenter;
        var edge = track.GroundHalfSize - 1;
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var position = ref positions[i].Value;
                ref var velocity = ref velocities[i].Value;

                // At the edge the kart stops going outwards, so it slides along it.
                var x = Math.Clamp(position.X, center.X - edge, center.X + edge);
                var z = Math.Clamp(position.Y, center.Z - edge, center.Z + edge);
                if (x != position.X) velocity.X = 0f;
                if (z != position.Y) velocity.Y = 0f;
                position = new Microsoft.Xna.Framework.Vector2(x, z);

                ref var state = ref karts[i];
                var onRoad = track.TryGetRoadHeight(new System.Numerics.Vector3(x, 0f, z), out state.Height);
                state.Surface = onRoad ? Surface.Asphalt : Surface.Grass;
            }
        }
    }
}
