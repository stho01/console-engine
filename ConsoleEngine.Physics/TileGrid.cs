using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Solid and empty tiles on a fixed grid: the level geometry of a tile-based game, without one entity per wall. Lookups
/// are O(1). Everything outside the grid is empty. Give it to <see cref="PhysicsSystem.TileGrid"/> so spatial queries
/// (<see cref="IPhysicsQueries"/>) and <see cref="CharacterMover"/> collide with it.
/// <para>
/// Phase 4 of <c>docs/design/physics-plan.md</c>: queries and character movement see the tiles; dynamic bodies don't
/// collide with them yet (use static box colliders for that).
/// </para>
/// </summary>
public sealed class TileGrid
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly bool[] _solid;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="columns">Width in tiles.</param>
    /// <param name="rows">Height in tiles.</param>
    /// <param name="tileSize">Size of one tile in cells. Tiles don't have to be square.</param>
    /// <param name="origin">World position of the top-left corner of tile (0, 0).</param>
    public TileGrid(int columns, int rows, Vector2 tileSize, Vector2 origin = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        if (!(tileSize.X > 0f) || !(tileSize.Y > 0f) || !float.IsFinite(tileSize.X) || !float.IsFinite(tileSize.Y))
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "Must be positive and finite.");

        Columns = columns;
        Rows = rows;
        TileSize = tileSize;
        Origin = origin;
        _solid = new bool[columns * rows];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Columns { get; }
    public int Rows { get; }
    public Vector2 TileSize { get; }
    public Vector2 Origin { get; }

    /// <summary>The category bits of the tiles, for query masks. Default <see cref="Collider.DefaultCategory"/>.</summary>
    public uint Category { get; set; } = Collider.DefaultCategory;

    //**********************************************************
    //** public methods
    //**********************************************************

    public bool IsSolid(int column, int row) =>
        (uint)column < (uint)Columns && (uint)row < (uint)Rows && _solid[row * Columns + column];

    public void SetSolid(int column, int row, bool solid = true)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)column, (uint)Columns, nameof(column));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, (uint)Rows, nameof(row));
        _solid[row * Columns + column] = solid;
    }

    /// <summary>The tile column a world x falls in (floored, so it can be outside the grid).</summary>
    public int ColumnOf(float x) => (int)MathF.Floor((x - Origin.X) / TileSize.X);

    /// <summary>The tile row a world y falls in (floored, so it can be outside the grid).</summary>
    public int RowOf(float y) => (int)MathF.Floor((y - Origin.Y) / TileSize.Y);

    /// <summary>World position of a tile's top-left corner.</summary>
    public Vector2 TileMin(int column, int row) => Origin + new Vector2(column * TileSize.X, row * TileSize.Y);

    /// <summary>Whether any solid tile overlaps the box from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public bool Overlaps(Vector2 min, Vector2 max)
    {
        // Touching a tile's edge isn't overlapping it, so a box exactly on a tile boundary stays out of that tile.
        var firstColumn = Math.Max(ColumnOf(min.X), 0);
        var firstRow = Math.Max(RowOf(min.Y), 0);
        var lastColumn = Math.Min(LastIndexBefore(max.X - Origin.X, TileSize.X), Columns - 1);
        var lastRow = Math.Min(LastIndexBefore(max.Y - Origin.Y, TileSize.Y), Rows - 1);
        for (var row = firstRow; row <= lastRow; row++)
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            if (_solid[row * Columns + column])
                return true;
        }

        return false;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The last tile index whose start is before <paramref name="offset"/> (an edge exactly on a boundary
    /// belongs to the tile before it).</summary>
    internal static int LastIndexBefore(float offset, float size) => (int)MathF.Ceiling(offset / size) - 1;
}
