namespace Kartster.Driving;

/// <summary>What the kart drives on. Values from Kartster's Circuit palette (<c>SurfacePalette.Circuit</c>).</summary>
public enum Surface
{
    Asphalt,
    Kerb,
    Gravel,
    Grass,
}

public static class SurfaceHandling
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Multiplier on <see cref="KartTuning.TyreFriction"/> and <see cref="KartTuning.TractionFriction"/>.</summary>
    public static float GetGripMultiplier(Surface surface) => surface switch
    {
        Surface.Kerb => 0.85f,
        Surface.Gravel => 0.7f,
        Surface.Grass => 0.6f,
        _ => 1f,
    };

    /// <summary>Multiplier on <see cref="KartTuning.RollingResistance"/>.</summary>
    public static float GetRollingDragMultiplier(Surface surface) => surface switch
    {
        Surface.Gravel => 3f,
        Surface.Grass => 2f,
        _ => 1f,
    };

    /// <summary>Share of the kart's own top speed the surface allows; the engine is governed to it.</summary>
    public static float GetTopSpeedFraction(Surface surface) => surface switch
    {
        Surface.Gravel => 0.4f,
        Surface.Grass => 0.5f,
        _ => 1f,
    };
}
