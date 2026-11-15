using System.Numerics;

namespace Kartster.Race;

/// <summary>
/// One place on the starting grid: where the kart's centre stands (x = world X, y = world Z) and its heading (0 faces
/// -Z, positive is turned left, as <c>KartEntity.Create</c> takes it).
/// </summary>
public readonly record struct GridSlot(Vector2 Position, float Heading);
