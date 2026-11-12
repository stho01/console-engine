using Vikingferd.Components;

namespace Vikingferd.Puzzle;

/// <summary>Where a hero or crate starts: the map tile its feet (a crate: the crate itself) are in.</summary>
public readonly record struct Spawn(ActorKind Kind, int Column, int Row);
