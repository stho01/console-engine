using System;

namespace SkyStack.Climbing;

/// <summary>
/// One platform of the tower: the pixel columns <see cref="Left"/> up to (not including) <see cref="Right"/>.
/// </summary>
public readonly record struct Floor(int Left, int Width)
{
    public int Right => Left + Width;

    /// <summary>
    /// True when a body <paramref name="bodyWidth"/> pixels wide, with its left edge at <paramref name="x"/>, has at
    /// least one pixel column over this platform.
    /// </summary>
    public bool Supports(float x, int bodyWidth)
    {
        var first = (int)MathF.Floor(x);
        return first + bodyWidth > Left && first < Right;
    }
}
