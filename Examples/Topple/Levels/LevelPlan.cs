using Microsoft.Xna.Framework;

namespace Topple.Levels;

public enum PieceShape : byte
{
    /// <summary>A turning box of <see cref="Piece.HalfSize"/>.</summary>
    Box,

    /// <summary>A triangle 6 wide and 3 high, its position at its centroid (1 above its base).</summary>
    Roof,

    /// <summary>A 2×2 target.</summary>
    Crystal
}

/// <summary>One body of a tower, placed by its centre.</summary>
public readonly record struct Piece(PieceShape Shape, Material Material, Vector2 Center, Vector2 HalfSize);

/// <summary>A rectangle of tiles, columns and rows inclusive.</summary>
public readonly record struct TileArea(int Left, int Top, int Right, int Bottom);

/// <summary>A kinematic raft over water that sways around <see cref="Anchor"/>.</summary>
public readonly record struct FerryPlan(Vector2 Anchor, Vector2 HalfSize, float Amplitude, float PeriodSeconds);

/// <summary>
/// A level: the ground (<see cref="LevelBuilder"/> adds the flat ground, the walls and the catapult hill), the water
/// pits cut into it, the towers and the shots the player gets.
/// </summary>
public sealed class LevelPlan
{
    public string Name { get; init; }
    public int Stones { get; init; }
    public int Bombs { get; init; }

    /// <summary>Extra solid ground on top of the flat ground.</summary>
    public TileArea[] Hills { get; init; } = [];

    /// <summary>Holes cut into the ground. Their lower part is water that sinks what falls in.</summary>
    public TileArea[] Pits { get; init; } = [];

    public Piece[] Pieces { get; init; } = [];

    public FerryPlan? Ferry { get; init; }
}
