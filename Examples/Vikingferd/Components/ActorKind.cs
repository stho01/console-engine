namespace Vikingferd.Components;

/// <summary>What an <see cref="Actor"/> is: one of the three heroes, or a crate.</summary>
public enum ActorKind : byte
{
    /// <summary>The jumper: jumps three tiles high and runs fastest.</summary>
    Ylva,

    /// <summary>The strong one: pushes crates and smashes cracked stone.</summary>
    Gorm,

    /// <summary>The glider: holding jump in the air slows her fall, so she survives any drop.</summary>
    Sigrid,

    /// <summary>A wooden crate: falls, can be stood on, presses plates. Only Gorm can push it.</summary>
    Crate
}
