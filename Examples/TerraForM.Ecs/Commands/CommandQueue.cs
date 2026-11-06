using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TerraForM.Ecs.Commands;

/// <summary>
/// The level's command sequences. Keys add commands to <see cref="Pending"/>. A commit moves them to the running queue,
/// where they run one at a time, in order. Commands are managed data shared by several systems, so they live here
/// rather than in components.
/// </summary>
public sealed class CommandQueue
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly List<RoverCommand> _pending = new();
    private readonly Queue<RoverCommand> _running = new();
    private double _currentElapsedMs;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Commands typed but not committed yet.</summary>
    public ReadOnlySpan<RoverCommand> Pending => CollectionsMarshal.AsSpan(_pending);

    public bool IsRunning => _running.Count > 0;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(RoverCommand command) => _pending.Add(command);

    /// <summary>Moves every pending command to the running queue. Returns false if there was nothing to commit.</summary>
    public bool Commit()
    {
        if (_pending.Count == 0)
            return false;

        for (var i = 0; i < _pending.Count; i++)
            _running.Enqueue(_pending[i]);

        _pending.Clear();
        return true;
    }

    public bool TryGetCurrent(out RoverCommand command) => _running.TryPeek(out command);

    /// <summary>
    /// Adds this frame's time to the current command, after it ran. Moves on to the next command once the current one
    /// has run for longer than its duration.
    /// </summary>
    public void Advance(double elapsedMs)
    {
        _currentElapsedMs += elapsedMs;
        if (_currentElapsedMs <= _running.Peek().DurationMs)
            return;

        _running.Dequeue();
        _currentElapsedMs = 0;
    }
}
