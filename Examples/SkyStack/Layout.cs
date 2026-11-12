namespace SkyStack;

/// <summary>
/// Screen sizes in console cells, and the tower in pixels. A cell shows two square pixels, one above the other
/// (<see cref="Rendering.PixelCanvas"/>), so the font is twice as tall as it is wide. The tower (walls included) fills
/// the left part of the screen, the text HUD the right part. World pixels grow downwards: floor <c>n</c> lies on world
/// row <c>-n * FloorSpacing</c>, so climbing means going to smaller rows.
/// </summary>
public static class Layout
{
    //**********************************************************
    //** console
    //**********************************************************

    public const int ScreenWidth = 112;
    public const int ScreenHeight = 60;
    public const int FontWidth = 6;
    public const int FontHeight = 12;

    /// <summary>First cell column of the HUD panel.</summary>
    public const int HudLeft = TowerWidth + 3;

    //**********************************************************
    //** pixels
    //**********************************************************

    public const int PixelsPerCellRow = 2;

    /// <summary>Pixel rows on screen.</summary>
    public const int ViewHeight = ScreenHeight * PixelsPerCellRow;

    /// <summary>Width of the tower in pixels (one per cell column), both walls included.</summary>
    public const int TowerWidth = 84;

    /// <summary>Pixels of each side wall.</summary>
    public const int WallWidth = 4;

    /// <summary>First pixel column inside the tower.</summary>
    public const int InnerLeft = WallWidth;

    /// <summary>First pixel column of the right wall (exclusive end of the inside).</summary>
    public const int InnerRight = TowerWidth - WallWidth;

    public const int InnerWidth = InnerRight - InnerLeft;

    /// <summary>Pixel rows from one floor to the next.</summary>
    public const int FloorSpacing = 16;

    /// <summary>Pixel rows of a platform, from its top (the floor's row) down.</summary>
    public const int PlatformThickness = 3;

    /// <summary>Screen pixel row of the ground floor when a run starts.</summary>
    public const int GroundViewRow = ViewHeight - 12;
}
