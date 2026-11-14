using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Effects;

namespace Kartster.Systems;

/// <summary>
/// Lays skid marks and puffs slide smoke from every kart's axle slip (<see cref="KartDynamics.FrontSlip"/>,
/// <see cref="KartDynamics.RearSlip"/>), once per frame like Kartster's effects. All karts share one ring of marks and
/// one pool of puffs, so the cost stays bounded however many karts there are. Uses the drawn (interpolated) pose, so
/// add it after the <c>InterpolationSystem</c> and before the <see cref="KartRenderSystem"/>.
/// </summary>
public sealed class KartEffectsSystem(KartPhysicsSystem physics, SkidMarks skidMarks, SlideSmoke smoke)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _karts;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _karts = World.Query(Query.With<Kart, Position, Rotation>().With<Velocity, RenderOffset>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var positions = chunk.GetSpan<Position>();
            var rotations = chunk.GetSpan<Rotation>();
            var velocities = chunk.GetSpan<Velocity>();
            var offsets = chunk.GetSpan<RenderOffset>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var ground = positions[i].Value + offsets[i].Position;
                var radians = rotations[i].Radians + offsets[i].Radians;
                var state = karts[i];
                var velocity2 = velocities[i].Value;

                var position = new Vector3(ground.X, state.Height, ground.Y);
                var forward = new Vector3(MathF.Sin(radians), 0f, -MathF.Cos(radians)); // as KartRenderSystem
                var right = Vector3.Cross(forward, Vector3.UnitY);
                var dynamics = physics.GetDynamics(state.Index);
                var wheels = new KartWheels(position, forward, dynamics.Tuning);

                skidMarks.Update(wheels, dynamics.FrontSlip, dynamics.RearSlip, state.Surface == Surface.Asphalt,
                    state.Index);
                smoke.Emit(wheels, new Vector3(velocity2.X, 0f, velocity2.Y), right, dynamics.FrontSlip,
                    dynamics.RearSlip, deltaSeconds, state.Index);
            }
        }

        smoke.Advance(deltaSeconds);
    }
}
