using Doom.Play;

namespace Doom.Components;

/// <summary>
/// What a thing is and what it is doing. Every thing in a level has one, the player's body too. The kind is a
/// <see cref="ThingInfo.Id"/>: <see cref="ThingInfo.ById"/> gives its sprite, size and behaviour.
/// </summary>
public struct Thing
{
    /// <summary>The <see cref="ThingInfo.Id"/> of its kind.</summary>
    public int Type;

    /// <summary>Starts as <see cref="ThingInfo.Flags"/>; a corpse loses <see cref="ThingFlags.Solid"/> and
    /// <see cref="ThingFlags.Shootable"/>.</summary>
    public ThingFlags Flags;

    public ThingState State;
    public int Health;

    /// <summary>Gone from play (picked up, finished exploding). The entity is destroyed at the end of the tic.</summary>
    public bool Removed;

    /// <summary>Dropped by a dead monster: ammo gives half.</summary>
    public bool Dropped;

    /// <summary>Map flag: waits for sight, not sound.</summary>
    public bool Ambush;
}
