namespace Doom;

/// <summary>
/// Screen sizes. The 3D view is a <see cref="ViewWidth"/> × <see cref="ViewHeight"/> pixel frame shown two pixels per
/// console cell (upper half block), so with a 6 × 12 font the pixels are square. The text HUD fills the rows below it.
/// </summary>
public static class Layout
{
    //**********************************************************
    //** console
    //**********************************************************

    public const int FontWidth = 6;
    public const int FontHeight = 12;

    /// <summary>Console columns; one view pixel per column.</summary>
    public const int ScreenWidth = ViewWidth;

    /// <summary>Console rows: the view plus the HUD.</summary>
    public const int ScreenHeight = ViewRows + HudRows;

    /// <summary>Console rows taken by the view.</summary>
    public const int ViewRows = ViewHeight / PixelsPerCellRow;

    /// <summary>Console rows of the text HUD below the view.</summary>
    public const int HudRows = 6;

    /// <summary>First console row of the HUD.</summary>
    public const int HudTop = ViewRows;

    //**********************************************************
    //** pixels
    //**********************************************************

    public const int PixelsPerCellRow = 2;

    /// <summary>View width in pixels: half of Doom's 320.</summary>
    public const int ViewWidth = 160;

    /// <summary>View height in pixels: half of Doom's 200.</summary>
    public const int ViewHeight = 100;
}
