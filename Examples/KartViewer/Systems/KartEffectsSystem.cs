using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using KartViewer.Components;
using KartViewer.Driving;
using KartViewer.Effects;

namespace KartViewer.Systems;

/// <summary>
/// Lays skid marks and puffs slide smoke from the kart's axle slip (<see cref="KartDynamics.FrontSlip"/>,
/// <see cref="KartDynamics.RearSlip"/>), once per frame like Kartster's effects. Uses the drawn (interpolated) pose,
/// so add it after the <c>InterpolationSystem</c> and before the <see cref="KartRenderSystem"/>.
/// </summary>
public sealed class KartEffectsSystem(Entity kart, KartTuning tuning, KartPhysicsSystem physics, SkidMarks skidMarks,
    SlideSmoke smoke) : EntitySystem
{
    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        var offset = World.Get<RenderOffset>(kart);
        var ground = World.Get<Position>(kart).Value + offset.Position;
        var radians = World.Get<Rotation>(kart).Radians + offset.Radians;
        var state = World.Get<Kart>(kart);
        var velocity2 = World.Get<Velocity>(kart).Value;

        var position = new Vector3(ground.X, state.Height, ground.Y);
        var forward = new Vector3(MathF.Sin(radians), 0f, -MathF.Cos(radians)); // as KartRenderSystem
        var right = Vector3.Cross(forward, Vector3.UnitY);
        var wheels = new KartWheels(position, forward, tuning);
        var dynamics = physics.Dynamics;

        skidMarks.Update(wheels, dynamics.FrontSlip, dynamics.RearSlip, state.Surface == Surface.Asphalt);
        smoke.Update(wheels, new Vector3(velocity2.X, 0f, velocity2.Y), right, dynamics.FrontSlip, dynamics.RearSlip,
            deltaSeconds);
    }
}
