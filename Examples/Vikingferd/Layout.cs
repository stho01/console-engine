using Microsoft.Xna.Framework;

namespace Vikingferd;

/// <summary>
/// Where things are on screen. A level is a grid of tiles, <see cref="TileWidth"/> x <see cref="TileHeight"/> console
/// cells each (square with the 8 x 16 font). The whole level is always on screen: no camera, so every part of a
/// puzzle is visible. Rows 0-1 are the header, then the stage area, then the hero panel and the key help.
/// </summary>
public static class Layout
{
    //**********************************************************
    //** screen
    //**********************************************************

    public const int ScreenWidth = 120;
    public const int ScreenHeight = 40;

    //**********************************************************
    //** tiles
    //**********************************************************

    public const int TileWidth = 4;
    public const int TileHeight = 2;

    /// <summary>The largest level that fits on screen, in tiles.</summary>
    public const int MaxStageColumns = ScreenWidth / TileWidth;

    public const int MaxStageRows = 16;

    //**********************************************************
    //** areas (screen rows)
    //**********************************************************

    public const int HeaderRow = 0;
    public const int HintRow = 1;
    public const int StageTop = 2;
    public const int StageAreaHeight = MaxStageRows * TileHeight;

    /// <summary>First row of the three hero cards (name, ability).</summary>
    public const int PanelRow = StageTop + StageAreaHeight + 1;

    public const int HeroCardWidth = ScreenWidth / 3;
    public const int KeyHelpRow = ScreenHeight - 1;

    //**********************************************************
    //** helpers
    //**********************************************************

    /// <summary>Screen cell of tile (0, 0) for a level of the given size: centred in the stage area.</summary>
    public static Point StageOrigin(int columns, int rows) =>
        new((ScreenWidth - columns * TileWidth) / 2, StageTop + (StageAreaHeight - rows * TileHeight) / 2);

    /// <summary>Canvas pixels per tile, both ways (a cell is one pixel wide and two high).</summary>
    public const int TilePixels = 4;

    /// <summary>Canvas pixel of tile (0, 0)'s top-left corner.</summary>
    public static Point StagePixelOrigin(int columns, int rows)
    {
        var origin = StageOrigin(columns, rows);
        return new Point(origin.X, origin.Y * 2);
    }
}
