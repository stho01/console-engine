using Platformer.Ecs.Utils;

namespace Platformer.Ecs.Components;

/// <summary>What the last collision check of a <see cref="PlayerBody"/> saw. Only read by the debug overlay.</summary>
public struct CollisionProbe
{
    /// <summary>The box at the position that was moved to, before it was pushed out of walls.</summary>
    public RectangleF Bounds;

    /// <summary>Corner tiles, moved inwards vertically. They decide whether horizontal movement is blocked.</summary>
    public CornerTiles Horizontal;

    /// <summary>Corner tiles, moved inwards horizontally. They decide whether vertical movement is blocked.</summary>
    public CornerTiles Vertical;
}

/// <summary>One <see cref="TileMap"/> character per corner of a box.</summary>
public struct CornerTiles
{
    public char RightBottom;
    public char LeftBottom;
    public char RightTop;
    public char LeftTop;
}
