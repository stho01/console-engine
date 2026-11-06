using Asteroids.Ecs.Components;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Inputs;

namespace Asteroids.Ecs.Systems;

/// <summary>A/D steer, W thrusts, SPACE fires. Applies drag to every ship.</summary>
public sealed class ShipControlSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _ships;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _ships = World.Query(Query.With<Ship, Rotation, Velocity>().With<Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var steering = 0f;
        if (Input.Instance.GetKey(Key.A).Held) steering -= 1f;
        if (Input.Instance.GetKey(Key.D).Held) steering += 1f;
        var thrusting = Input.Instance.GetKey(Key.W).Held;
        var firing = Input.Instance.GetKey(Key.SPACE).Pressed;

        foreach (var chunk in _ships.GetChunks())
        {
            var ships = chunk.GetSpan<Ship>();
            var rotations = chunk.GetSpan<Rotation>();
            var velocities = chunk.GetSpan<Velocity>();
            var positions = chunk.GetSpan<Position>();

            for (var i = 0; i < chunk.Count; i++)
            {
                ref var velocity = ref velocities[i].Value;
                rotations[i].Radians += steering * ships[i].SteeringSpeed * deltaSeconds;
                var heading = rotations[i].Heading;

                if (thrusting)
                    velocity += heading * ships[i].Thrust * deltaSeconds;
                velocity -= velocity * ships[i].Drag * deltaSeconds;

                if (firing)
                    Spawner.CreateBullet(Commands, positions[i].Value, heading, velocity);
            }
        }
    }
}
