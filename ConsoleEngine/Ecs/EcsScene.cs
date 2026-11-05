using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Scenery;

namespace ConsoleEngine.Ecs;

/// <summary>
/// A scene that owns a <see cref="World"/> and two system groups. <see cref="Simulation"/> runs in the scene's
/// update, <see cref="Presentation"/> in its render. The world is created on load and disposed on unload, which frees
/// its native memory. Add entities and systems in <see cref="OnCreate"/>.
/// </summary>
public abstract class EcsScene<TGame> : Scene<TGame> where TGame : GameBase
{
    //**********************************************************
    //** props
    //**********************************************************

    protected World World { get; private set; }

    /// <summary>Game logic: input, movement, collisions, lifetimes.</summary>
    protected SystemGroup Simulation { get; private set; }

    /// <summary>Drawing. Runs after the frame buffer was cleared and the simulation finished.</summary>
    protected SystemGroup Presentation { get; private set; }

    /// <summary>What part of the world is on screen. Move it from a system to follow the player.</summary>
    protected Camera Camera { get; } = new();

    //**********************************************************
    //** lifecycle
    //**********************************************************

    /// <summary>Create entities and add systems here. <see cref="World"/> and both groups exist already.</summary>
    protected abstract void OnCreate();

    /// <summary>Called on unload, before the world is disposed.</summary>
    protected virtual void OnDestroy() { }

    public sealed override void OnLoad()
    {
        World = new World();
        Simulation = new SystemGroup(World);
        Presentation = new SystemGroup(World);
        OnCreate();
    }

    public sealed override void OnUnload()
    {
        OnDestroy();
        World.Dispose();
    }

    public sealed override void OnUpdate() => Simulation.Update(DeltaSeconds);

    public sealed override void OnRender() => Presentation.Update(DeltaSeconds);

    //**********************************************************
    //** internals
    //**********************************************************

    private static float DeltaSeconds => (float)GameTime.Delta.TotalSeconds;
}
