using System;

namespace EternalDigger.Effects;

/// <summary>How hard the camera shakes right now, in console cells. Effects add to it, the camera decays it.</summary>
public sealed class ScreenShake
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float MaxStrength = 2f;

    /// <summary>Strength lost per second.</summary>
    public const float Decay = 6f;

    //**********************************************************
    //** props
    //**********************************************************

    public float Strength { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(float strength) => Strength = Math.Min(MaxStrength, Strength + strength);

    public void Update(float deltaSeconds) => Strength = Math.Max(0f, Strength - Decay * deltaSeconds);
}
