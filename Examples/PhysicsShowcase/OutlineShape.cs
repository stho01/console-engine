using System;
using Microsoft.Xna.Framework;

namespace PhysicsShowcase;

/// <summary>
/// Turns a collider's corners into the corners of the outline that draws it. The outline system draws a corner in the
/// cell it lies in, so a box resting on a floor at y = 28 would have its bottom edge (y ≈ 28.005, after settling
/// <c>LinearSlop</c> into the floor) drawn in row 28, inside the floor. Moving every corner half a cell towards the
/// body's origin on each axis draws the outline through the centres of the outer cells the shape covers instead.
/// </summary>
public static class OutlineShape
{
    private const float HalfCell = .5f;

    public static Vector2[] ThroughCellCentres(ReadOnlySpan<Vector2> corners)
    {
        var outline = new Vector2[corners.Length];
        for (var i = 0; i < corners.Length; i++)
            outline[i] = new Vector2(Inset(corners[i].X), Inset(corners[i].Y));
        return outline;
    }

    private static float Inset(float value) => MathF.Sign(value) * MathF.Max(MathF.Abs(value) - HalfCell, 0f);
}
