using System;
using Microsoft.Xna.Framework;

namespace Topple.Rendering;

/// <summary>
/// Trauma-based screen shake: hits add trauma (0 to 1), it fades over time, and the offset grows with trauma², so small
/// hits barely move the screen and a blast rattles it.
/// </summary>
public sealed class ScreenShake(Random random)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float MaxOffset = 2f;

    /// <summary>Trauma lost per second.</summary>
    public const float Recovery = 1.6f;

    //**********************************************************
    //** props
    //**********************************************************

    public float Trauma { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(float trauma) => Trauma = MathF.Min(1f, Trauma + trauma);

    /// <summary>Fades the trauma and returns this frame's offset in whole cells.</summary>
    public Point Update(float deltaSeconds)
    {
        Trauma = MathF.Max(0f, Trauma - Recovery * deltaSeconds);
        if (Trauma <= 0f)
            return Point.Zero;

        var strength = Trauma * Trauma * MaxOffset;
        return new Point((int)MathF.Round((random.NextSingle() * 2f - 1f) * strength),
            (int)MathF.Round((random.NextSingle() * 2f - 1f) * strength));
    }
}
