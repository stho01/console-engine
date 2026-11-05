using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using Platformer.Ecs.Components;

namespace Platformer.Ecs.Systems;

/// <summary>A/D run (weaker in the air), SPACE requests a jump. <see cref="PlayerPhysicsSystem"/> applies both.</summary>
public sealed class PlayerControlSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _players;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _players = World.Query(Query.With<PlayerBody>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var direction = 0f;
        if (Input.Instance.GetKey(Key.A).Held) direction -= 1f;
        if (Input.Instance.GetKey(Key.D).Held) direction += 1f;
        var jumping = Input.Instance.GetKey(Key.SPACE).Pressed;

        foreach (var chunk in _players.GetChunks())
        {
            var bodies = chunk.GetSpan<PlayerBody>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var body = ref bodies[i];
                var strength = body.IsAirborne ? body.AirAcceleration : body.GroundAcceleration;
                body.Acceleration.X += direction * strength;
                if (jumping)
                    body.JumpRequested = true;
            }
        }
    }
}
