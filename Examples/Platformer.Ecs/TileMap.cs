using System;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;

namespace Platformer.Ecs;

/// <summary>
/// The level as a grid of characters. Each map cell is one tile of <see cref="TileWidth"/> x <see cref="TileHeight"/>
/// console cells. Collisions use <see cref="ToTileGrid"/>; walls are also spawned as entities, but only to draw them.
/// </summary>
public sealed class TileMap
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Returned for coordinates outside the map. Not a wall.</summary>
    public const char Outside = ' ';

    private readonly string[] _rows;

    //**********************************************************
    //** ctor
    //**********************************************************

    public TileMap(string[] rows, int tileWidth, int tileHeight)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = rows;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Height = rows.Length;
        for (var y = 0; y < rows.Length; y++)
            Width = Math.Max(Width, rows[y].Length);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Columns of the widest row.</summary>
    public int Width { get; }

    public int Height { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public static bool IsWall(char tile) => tile is not ('.' or Outside);

    /// <summary>The walls as the engine's collision grid, tile (0, 0) at world (0, 0).</summary>
    public TileGrid ToTileGrid()
    {
        var grid = new TileGrid(Width, Height, new Vector2(TileWidth, TileHeight));
        for (var row = 0; row < Height; row++)
        for (var column = 0; column < _rows[row].Length; column++)
        {
            if (IsWall(_rows[row][column]))
                grid.SetSolid(column, row);
        }

        return grid;
    }

    /// <summary>The tile at a map column and row, or <see cref="Outside"/>.</summary>
    public char GetTileAt(int column, int row)
    {
        if (column >= 0 && row >= 0 && row < Height && column < _rows[row].Length)
            return _rows[row][column];

        return Outside;
    }
}
