namespace Platformer.Ecs.Components;

// Tags: no fields, so they cost no memory per entity. They only put entities in their own archetype, which queries
// can select with With<T>() or skip with Without<T>().

/// <summary>A wall of the <see cref="TileMap"/>. Drawn after the player, as in <c>Examples/Platformer</c>.</summary>
public struct Tile;
