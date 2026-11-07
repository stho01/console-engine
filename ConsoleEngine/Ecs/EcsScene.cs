using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Scenery;

namespace ConsoleEngine.Ecs;

/// <summary>
/// A scene that owns a <see cref="World"/> and three system groups. The scene's update runs
/// <see cref="FixedSimulation"/> zero or more times at a fixed rate, then <see cref="Simulation"/> once with the frame
/// time. <see cref="Presentation"/> runs in its render. The world is created on load and disposed on unload, which
/// frees its native memory. Add entities and systems in <see cref="OnCreate"/>.
/// </summary>
public abstract class EcsScene<TGame> : Scene<TGame> where TGame : GameBase
{
    //**********************************************************
    //** props
    //**********************************************************

    protected World World { get; private set; }

    /// <summary>
    /// Physics and the game logic that reacts to it. Runs <see cref="FixedTimestep.Advance"/> times per frame, each
    /// time with <see cref="FixedTimestep.StepSeconds"/>, before <see cref="Simulation"/>. At a high frame rate most
    /// frames run it zero times, so systems here must not read per-frame input edges directly: latch them in
    /// <see cref="Simulation"/> and consume them here.
    /// </summary>
    protected SystemGroup FixedSimulation { get; private set; }

    /// <summary>
    /// The rate of <see cref="FixedSimulation"/>. A new default one is created on load, before <see cref="OnCreate"/>,
    /// which may replace it.
    /// </summary>
    protected FixedTimestep FixedTimestep { get; set; }

    /// <summary>Game logic: input, movement, collisions, lifetimes.</summary>
    protected SystemGroup Simulation { get; private set; }

    /// <summary>Drawing. Runs after the frame buffer was cleared and the simulation finished.</summary>
    protected SystemGroup Presentation { get; private set; }

    /// <summary>What part of the world is on screen. Move it from a system to follow the player.</summary>
    protected Camera Camera { get; } = new();

    //**********************************************************
    //** lifecycle
    //**********************************************************

    /// <summary>Create entities and add systems here. <see cref="World"/> and all groups exist already.</summary>
    protected abstract void OnCreate();

    /// <summary>Called on unload, before the world is disposed.</summary>
    protected virtual void OnDestroy() { }

    public sealed override void OnLoad()
    {
        World = new World();
        FixedSimulation = new SystemGroup(World);
        FixedTimestep = new FixedTimestep();
        Simulation = new SystemGroup(World);
        Presentation = new SystemGroup(World);
        OnCreate();
    }

    public sealed override void OnUnload()
    {
        OnDestroy();
        World.Dispose();
    }

    public sealed override void OnUpdate() => RunSimulation(DeltaSeconds);

    public sealed override void OnRender() => Presentation.Update(DeltaSeconds);

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>One frame of simulation: the fixed steps that are due, then <see cref="Simulation"/>.</summary>
    internal void RunSimulation(float deltaSeconds)
    {
        var steps = FixedTimestep.Advance(deltaSeconds);
        var stepSeconds = FixedTimestep.StepSeconds;
        for (var i = 0; i < steps; i++)
            FixedSimulation.Update(stepSeconds);

        Simulation.Update(deltaSeconds);
    }

    private static float DeltaSeconds => (float)GameTime.Delta.TotalSeconds;
}
