namespace Starguard.Components;

// Tags: no fields, so they cost no memory per entity. They put shots in their own archetypes, which queries select
// with With<T>().

/// <summary>A shot fired by the cannon, moving up. Only one is in flight at a time.</summary>
public struct PlayerShot;

/// <summary>A shot dropped by an alien, moving down.</summary>
public struct AlienShot;
