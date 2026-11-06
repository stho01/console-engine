using System;
using System.Collections.Generic;
using System.Reflection;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Runs its systems, then plays back the shared <see cref="CommandBuffer"/>. That playback is the group's sync point:
/// entities created or destroyed by any system become visible to the next group (or the next frame).
/// <para>
/// Systems run in the order they were added, except where <see cref="UpdateBeforeAttribute"/> or
/// <see cref="UpdateAfterAttribute"/> on a system's class requires otherwise. The order is worked out once after
/// systems were added, not every frame.
/// </para>
/// </summary>
public sealed class SystemGroup
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly World _world;

    /// <summary>In Add order.</summary>
    private readonly List<EntitySystem> _systems = new();

    /// <summary><see cref="_systems"/> sorted by the ordering attributes. Rebuilt when a system is added.</summary>
    private readonly List<EntitySystem> _runOrder = new();
    private bool _runOrderIsStale;

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

    /// <summary>The order the systems run in. Throws if their ordering attributes form a cycle.</summary>
    public IReadOnlyList<EntitySystem> RunOrder
    {
        get
        {
            EnsureRunOrder();
            return _runOrder;
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Appends a system and calls its <c>OnCreate</c>. Returns it for chaining.</summary>
    public TSystem Add<TSystem>(TSystem system) where TSystem : EntitySystem
    {
        ArgumentNullException.ThrowIfNull(system);
        system.Attach(_world, Commands);
        _systems.Add(system);
        _runOrderIsStale = true;
        return system;
    }

    public void Update(float deltaSeconds)
    {
        EnsureRunOrder();
        for (var i = 0; i < _runOrder.Count; i++)
        {
            if (_runOrder[i].Enabled)
                _runOrder[i].Update(deltaSeconds);
        }

        Commands.Playback(_world);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// Topological sort (Kahn) of the constraints. Among the systems that are free to run next, the one added first
    /// wins, so systems without constraints keep their Add order.
    /// </summary>
    private void EnsureRunOrder()
    {
        if (!_runOrderIsStale)
            return;

        var count = _systems.Count;
        var runsBefore = new List<int>[count];
        var waitingFor = new int[count];
        for (var i = 0; i < count; i++)
            runsBefore[i] = new List<int>();

        for (var i = 0; i < count; i++)
        {
            var type = _systems[i].GetType();
            foreach (var before in type.GetCustomAttributes<UpdateBeforeAttribute>())
                AddConstraints(i, before.SystemType, runsBefore, waitingFor, systemRunsFirst: true);
            foreach (var after in type.GetCustomAttributes<UpdateAfterAttribute>())
                AddConstraints(i, after.SystemType, runsBefore, waitingFor, systemRunsFirst: false);
        }

        _runOrder.Clear();
        var scheduled = new bool[count];
        while (_runOrder.Count < count)
        {
            var next = -1;
            for (var i = 0; i < count && next < 0; i++)
            {
                if (!scheduled[i] && waitingFor[i] == 0)
                    next = i;
            }

            if (next < 0)
                EcsThrowHelper.ThrowSystemOrderCycle(UnscheduledNames(scheduled));

            scheduled[next] = true;
            _runOrder.Add(_systems[next]);
            foreach (var later in runsBefore[next])
                waitingFor[later]--;
        }

        _runOrderIsStale = false;
    }

    /// <summary>Records "system runs before/after every system in the group that is an <paramref name="other"/>".</summary>
    private void AddConstraints(int system, Type other, List<int>[] runsBefore, int[] waitingFor, bool systemRunsFirst)
    {
        for (var j = 0; j < _systems.Count; j++)
        {
            if (j == system || !other.IsInstanceOfType(_systems[j]))
                continue;

            var first = systemRunsFirst ? system : j;
            var second = systemRunsFirst ? j : system;
            runsBefore[first].Add(second);
            waitingFor[second]++;
        }
    }

    private string UnscheduledNames(bool[] scheduled)
    {
        var names = new List<string>();
        for (var i = 0; i < _systems.Count; i++)
        {
            if (!scheduled[i])
                names.Add(_systems[i].GetType().Name);
        }

        return string.Join(", ", names);
    }
}
