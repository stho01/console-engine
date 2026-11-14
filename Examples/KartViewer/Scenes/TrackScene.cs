using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using KartViewer.Driving;
using KartViewer.Systems;
using Microsoft.Xna.Framework;

namespace KartViewer.Scenes;

/// <summary>
/// Drives the kart on the track: Kartster's kart model (<see cref="KartPhysicsSystem"/>) on the engine's
/// <see cref="PhysicsSystem"/> at Kartster's 120 steps per second, with a chase camera. The kart starts standing still
/// at route distance 0, facing along the route; the game restarts the scene for R and Tab.
/// </summary>
public sealed class TrackScene : EcsScene<KartViewerGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's fixed step (<c>PhysicsTuning</c>).</summary>
    public const float StepSeconds = 1f / 120f;

    /// <summary>Kartster's most steps per frame.</summary>
    public const int MaxStepsPerFrame = 8;

    private Entity _kart;
    private KartPhysicsSystem _physics;

    //**********************************************************
    //** props
    //**********************************************************

    public KartDynamics Dynamics => _physics.Dynamics;

    /// <summary>Metres per second.</summary>
    public float Speed => World.Get<Velocity>(_kart).Value.Length();

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var track = Game.Track;
        var (start, forward) = track.Sample(0);
        _kart = KartEntity.Create(World, Game.Tuning, new Vector2(start.X, start.Z), KartFrame.HeadingOf(forward));
        World.Get<Components.Kart>(_kart).Height = start.Y;

        FixedTimestep = new FixedTimestep(StepSeconds, MaxStepsPerFrame);
        _physics = FixedSimulation.Add(new KartPhysicsSystem(_kart, Game.Tuning));
        // One substep, so a step integrates the kart model's forces exactly once, as Kartster's single 1/120 s step
        // does (the force is held through all substeps, and the tyres cancel a whole step's sideways speed). The kart
        // touches nothing, so the solver has no contacts that would need more substeps; nothing sleeps either.
        FixedSimulation.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        FixedSimulation.Add(new KartGroundSystem(_kart, track));

        Simulation.Add(new KartInputSystem(_kart));

        Presentation.Add(new InterpolationSystem(FixedTimestep));
        Presentation.Add(new KartRenderSystem(_kart, Game));
    }
}
