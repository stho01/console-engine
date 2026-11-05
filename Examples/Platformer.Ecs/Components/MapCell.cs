namespace Platformer.Ecs.Components;

/// <summary>Column and row of a tile in the <see cref="TileMap"/>. Shown on the tile in debug mode.</summary>
public struct MapCell(int column, int row)
{
    public int Column = column;
    public int Row = row;
}
