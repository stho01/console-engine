namespace ConsoleEngine.Ecs;

/// <summary>
/// Game logic that runs over queries once per frame. Add it to a <see cref="SystemGroup"/>: the group sets
/// <see cref="World"/> and <see cref="Commands"/>, then calls <see cref="OnCreate"/>, where the system builds its
/// queries. Per-entity work belongs in chunk loops or <see cref="IForEach{T1}"/> jobs, not in virtual calls.
/// </summary>
public abstract class EntitySystem
{
    //**********************************************************
    //** props
    //**********************************************************

    protected World World { get; private set; }

    /// <summary>
    /// Structural changes to apply after the group has run. Use it instead of the world inside query loops.
    /// </summary>
    protected CommandBuffer Commands { get; private set; }

    /// <summary>A disabled system is skipped by its group.</summary>
    public bool Enabled { get; set; } = true;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    /// <summary>Called once when the system joins a group. Create queries here.</summary>
    protected virtual void OnCreate() { }

    protected abstract void OnUpdate(float deltaSeconds);

    //**********************************************************
    //** internals
    //**********************************************************

    internal void Attach(World world, CommandBuffer commands)
    {
        World = world;
        Commands = commands;
        OnCreate();
    }

    internal void Update(float deltaSeconds) => OnUpdate(deltaSeconds);
}
