using System;
using Microsoft.Xna.Framework;
using Vikingferd.Components;

namespace Vikingferd.Puzzle;

/// <summary>What can happen in a step that is worth showing (dust, debris, sparks).</summary>
public enum PuzzleEventKind : byte
{
    /// <summary>A cracked tile was smashed. Position: the tile's centre.</summary>
    Smashed,

    /// <summary>A lever was pulled. Position: the tile's centre; channel set.</summary>
    LeverPulled,

    /// <summary>A door opened or closed, or a bridge appeared or went. Position: the tile's centre; channel set.</summary>
    GateMoved,

    /// <summary>A hero jumped. Position: the feet.</summary>
    Jumped,

    /// <summary>A hero landed hard. Position: the feet.</summary>
    Landed,

    /// <summary>A crate landed. Position: the middle of its bottom edge.</summary>
    CrateLanded,

    /// <summary>A hero was knocked out. Position: the head.</summary>
    KnockedOut
}

public readonly record struct PuzzleEvent(PuzzleEventKind Kind, Vector2 Position, ActorKind Actor, int Channel);

/// <summary>
/// Events collected by <see cref="PuzzleRules.Step"/> until the effects read them. Fixed capacity, no allocations;
/// when full, further events are dropped (only effects are lost).
/// </summary>
public sealed class PuzzleEvents
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Capacity = 64;

    private readonly PuzzleEvent[] _events = new PuzzleEvent[Capacity];

    //**********************************************************
    //** props
    //**********************************************************

    public int Count { get; private set; }
    public ReadOnlySpan<PuzzleEvent> All => _events.AsSpan(0, Count);

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(PuzzleEventKind kind, Vector2 position, ActorKind actor = ActorKind.Crate, int channel = 0)
    {
        if (Count < Capacity)
            _events[Count++] = new PuzzleEvent(kind, position, actor, channel);
    }

    public void Clear() => Count = 0;
}
