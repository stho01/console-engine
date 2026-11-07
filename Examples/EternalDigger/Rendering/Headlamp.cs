namespace EternalDigger.Rendering;

/// <summary>
/// This frame's headlamp, worked out by <c>LampSystem</c> from the oil and the lamp tier and read by the lighting and
/// the miner's sprite: how far the cone reaches, whether this is a dim flicker frame, and whether the oil is low.
/// </summary>
public sealed class Headlamp
{
    /// <summary>Tiles the cone reaches. 0: no cone, only the close radius.</summary>
    public float Reach { get; set; } = LightMap.LampRange;

    /// <summary>A flicker frame: the lamp's light is one level darker.</summary>
    public bool IsDimmed { get; set; }

    /// <summary>Below <see cref="LampOil.LowShare"/>: the lamp glyph dims and smoke rises.</summary>
    public bool IsLow { get; set; }

    /// <summary>The tank is dry.</summary>
    public bool IsEmpty { get; set; }
}
