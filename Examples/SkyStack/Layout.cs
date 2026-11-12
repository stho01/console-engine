namespace SkyStack;

/// <summary>
/// Screen and tower sizes in console cells. The tower (walls included) fills the left part of the screen, the HUD the
/// right part. Rows grow downwards: floor <c>n</c> lies on world row <c>-n * FloorSpacing</c>, so climbing means
/// going to smaller rows.
/// </summary>
public static class Layout
{
    public const int ScreenWidth = 64;
    public const int ScreenHeight = 40;

    /// <summary>Columns of each side wall.</summary>
    public const int WallWidth = 2;

    /// <summary>Width of the tower, both walls included.</summary>
    public const int TowerWidth = 44;

    /// <summary>First column inside the tower.</summary>
    public const int InnerLeft = WallWidth;

    /// <summary>First column of the right wall (exclusive end of the inside).</summary>
    public const int InnerRight = TowerWidth - WallWidth;

    public const int InnerWidth = InnerRight - InnerLeft;

    /// <summary>First column of the HUD panel.</summary>
    public const int HudLeft = TowerWidth + 2;

    /// <summary>Rows from one floor to the next.</summary>
    public const int FloorSpacing = 4;

    /// <summary>Screen row of the ground floor when a run starts.</summary>
    public const int GroundScreenRow = ScreenHeight - 3;
}
