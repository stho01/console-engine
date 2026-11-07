using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger;

/// <summary>
/// Where things are. The world is a grid of tiles, <see cref="TileWidth"/> x <see cref="TileHeight"/> console cells
/// each (4 x 2 cells look square in a terminal and with the game's 8 x 16 Windows font). Tile row 0 is the top of the
/// sky. The mine is exactly as wide as the screen, so the camera only scrolls vertically.
/// </summary>
public static class Layout
{
    //**********************************************************
    //** screen
    //**********************************************************

    public const int ScreenWidth = Mine.Columns * TileWidth;
    public const int ScreenHeight = 48;

    /// <summary>Rows at the top of the screen covered by the HUD.</summary>
    public const int HudHeight = 2;

    //**********************************************************
    //** tiles
    //**********************************************************

    public const int TileWidth = 4;
    public const int TileHeight = 2;

    //**********************************************************
    //** surface (tile columns, all on row Mine.SurfaceRow - 1)
    //**********************************************************

    /// <summary>The row the miner walks on at the surface: the air just above the grass.</summary>
    public const int GroundRow = Mine.SurfaceRow - 1;

    /// <summary>First and last column covered by the house sprite. Decoration only, the miner walks in front.</summary>
    public const int HouseFirstColumn = 2;
    public const int HouseLastColumn = 11;

    /// <summary>Standing on these columns at <see cref="GroundRow"/> lets the miner deliver ore (the house door).</summary>
    public const int DeliverFirstColumn = 3;
    public const int DeliverLastColumn = 6;

    /// <summary>Standing on these columns at <see cref="GroundRow"/> puts the miner at the shop counter.</summary>
    public const int ShopFirstColumn = 8;
    public const int ShopLastColumn = 11;

    /// <summary>Standing on these columns at <see cref="GroundRow"/> lets the miner build lift segments.</summary>
    public const int LiftSignFirstColumn = 23;
    public const int LiftSignLastColumn = 24;

    /// <summary>The blasting shed's columns. Scenery: dynamite is sold at the shop counter.</summary>
    public const int BlastingFirstColumn = 26;
    public const int BlastingLastColumn = 28;

    /// <summary>Where the miner starts and where a rescue brings them back.</summary>
    public const int SpawnColumn = 15;

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>World position (console cells) of a tile's top-left cell.</summary>
    public static Vector2 TileToWorld(int column, int row) => new(column * TileWidth, row * TileHeight);

    /// <summary>World position (console cells) of a tile's centre.</summary>
    public static Vector2 TileCenter(int column, int row) =>
        new(column * TileWidth + TileWidth / 2f, row * TileHeight + TileHeight / 2f);

    public static bool IsAtDeliverSpot(int column, int row) =>
        row == GroundRow && column >= DeliverFirstColumn && column <= DeliverLastColumn;

    public static bool IsAtLiftSign(int column, int row) =>
        row == GroundRow && column >= LiftSignFirstColumn && column <= LiftSignLastColumn;

    public static bool IsAtShop(int column, int row) =>
        row == GroundRow && column >= ShopFirstColumn && column <= ShopLastColumn;
}
