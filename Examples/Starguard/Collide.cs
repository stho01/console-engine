using System;
using Microsoft.Xna.Framework;
using Starguard.Components;

namespace Starguard;

/// <summary>
/// Cell-based hit tests. Positions are floats, cells are their floor. A box covers <c>Width × Height</c> cells from
/// its top-left position.
/// </summary>
public static class Collide
{
    /// <summary>
    /// Whether a shot that moved straight up or down from <paramref name="previousY"/> to <paramref name="shot"/> this
    /// frame passed through the box. Testing the whole path means a fast shot at a low frame rate can't skip a row.
    /// </summary>
    public static bool ShotHits(Vector2 shot, float previousY, Vector2 boxPosition, Hitbox box)
    {
        var column = Cell(shot.X);
        var left = Cell(boxPosition.X);
        if (column < left || column >= left + box.Width)
            return false;

        var from = Cell(previousY);
        var to = Cell(shot.Y);
        var top = Cell(boxPosition.Y);
        return Math.Min(from, to) < top + box.Height && Math.Max(from, to) >= top;
    }

    /// <summary>Whether two boxes share at least one cell.</summary>
    public static bool Overlaps(Vector2 aPosition, Hitbox a, Vector2 bPosition, Hitbox b)
    {
        var aLeft = Cell(aPosition.X);
        var aTop = Cell(aPosition.Y);
        var bLeft = Cell(bPosition.X);
        var bTop = Cell(bPosition.Y);
        return aLeft < bLeft + b.Width && bLeft < aLeft + a.Width && aTop < bTop + b.Height && bTop < aTop + a.Height;
    }

    public static int Cell(float coordinate) => (int)MathF.Floor(coordinate);
}
