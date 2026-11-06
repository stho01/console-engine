using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using Microsoft.Xna.Framework;
using Swarm.Components;

namespace Swarm.Systems;

/// <summary>
/// Pulls every particle towards a point that drifts across the screen on a Lissajous curve, with some drag so the
/// swarm doesn't fly apart. Runs before the engine's movement.
/// </summary>
[UpdateBefore(typeof(MovementSystem))]
public sealed class AttractorSystem(int width, int height) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Fraction of velocity lost per second.</summary>
    private const float Drag = .3f;

    private Query _particles;
    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Agility, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        var job = new PullJob
        {
            Attractor = new Vector2(
                width * (.5f + .35f * MathF.Sin(_time * .7f)),
                height * (.5f + .35f * MathF.Sin(_time * 1.1f))),
            DeltaSeconds = deltaSeconds
        };
        _particles.ForEach<PullJob, Agility, Position, Velocity>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct PullJob : IForEach<Agility, Position, Velocity>
    {
        public Vector2 Attractor;
        public float DeltaSeconds;

        public void Execute(ref Agility agility, ref Position position, ref Velocity velocity)
        {
            var toAttractor = Attractor - position.Value;
            // +1 keeps the pull finite on top of the attractor.
            var direction = toAttractor / MathF.Sqrt(toAttractor.LengthSquared() + 1f);
            velocity.Value += direction * (agility.Pull * DeltaSeconds);
            velocity.Value -= velocity.Value * (Drag * DeltaSeconds);
        }
    }
}
