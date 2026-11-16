namespace Doom;

/// <summary>
/// Screen sizes. The 3D view is a <see cref="ViewWidth"/> × <see cref="ViewHeight"/> pixel frame shown two pixels per
/// console cell (upper half block), and the font is half as wide as it is tall, so the pixels are square. The text
/// HUD fills the rows below it. Picked at start-up (<see cref="ResolutionPrompt"/>): <see cref="Standard"/> or
/// <see cref="High"/>. Both fill the same window in conhost; <see cref="High"/> has half-size cells.
/// </summary>
public sealed class Layout
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int PixelsPerCellRow = 2;

    /// <summary>Console rows of the text HUD below the view.</summary>
    public const int HudRows = 6;

    /// <summary>Half of Doom's 320 × 200, in a 6 × 12 font. The default.</summary>
    public static readonly Layout Standard = new(160, 100, 6, 12);

    /// <summary>Doom's own 320 × 200, in a 3 × 6 font: 320 console columns.</summary>
    public static readonly Layout High = new(320, 200, 3, 6);

    //**********************************************************
    //** ctor
    //**********************************************************

    private Layout(int viewWidth, int viewHeight, int fontWidth, int fontHeight)
    {
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
        FontWidth = fontWidth;
        FontHeight = fontHeight;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>View width in pixels.</summary>
    public int ViewWidth { get; }

    /// <summary>View height in pixels.</summary>
    public int ViewHeight { get; }

    public int FontWidth { get; }
    public int FontHeight { get; }

    /// <summary>View pixels per <see cref="Standard"/> pixel along a row: 1 or 2.</summary>
    public float PixelScale => ViewWidth / (float)Standard.ViewWidth;

    /// <summary>Console columns; one view pixel per column.</summary>
    public int ScreenWidth => ViewWidth;

    /// <summary>Console rows: the view plus the HUD.</summary>
    public int ScreenHeight => ViewRows + HudRows;

    /// <summary>Console rows taken by the view.</summary>
    public int ViewRows => ViewHeight / PixelsPerCellRow;

    /// <summary>First console row of the HUD.</summary>
    public int HudTop => ViewRows;
}
