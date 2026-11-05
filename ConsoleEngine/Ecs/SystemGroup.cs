using System;
using System.Collections.Generic;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Runs its systems in the order they were added, then plays back the shared <see cref="CommandBuffer"/>. That
/// playback is the group's sync point: entities created or destroyed by any system become visible to the next group
/// (or the next frame).
/// </summary>
public sealed class SystemGroup
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly World _world;
    private readonly List<EntitySystem> _systems = new();

    //**********************************************************
    //** ctor
    //**********************************************************

    public SystemGroup(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public CommandBuffer Commands { get; } = new();
    public int Count => _systems.Count;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Appends a system and calls its <c>OnCreate</c>. Returns it for chaining.</summary>
    public TSystem Add<TSystem>(TSystem system) where TSystem : EntitySystem
    {
        ArgumentNullException.ThrowIfNull(system);
        system.Attach(_world, Commands);
        _systems.Add(system);
        return system;
    }

    public void Update(float deltaSeconds)
    {
        for (var i = 0; i < _systems.Count; i++)
        {
            if (_systems[i].Enabled)
                _systems[i].Update(deltaSeconds);
        }

        Commands.Playback(_world);
    }
}
