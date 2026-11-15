using System;

namespace Starguard;

/// <summary>
/// How the alien formation marches. All aliens step together: <see cref="StepCells"/> sideways, or
/// <see cref="DropCells"/> down and turn around when the next step would cross the edge. Each step also flips the
/// animation frame. Fewer aliens means shorter steps between pauses, so the last ones rush.
/// </summary>
public sealed class Formation
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int StepCells = 2;
    public const int DropCells = 1;

    /// <summary>Pause between steps while the whole wave is alive.</summary>
    public const float SlowestStepSeconds = 0.45f;

    /// <summary>Pause between steps when one alien is left.</summary>
    public const float FastestStepSeconds = 0.03f;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>+1 marching right, -1 marching left.</summary>
    public int Direction { get; set; } = 1;

    /// <summary>0 or 1: which of an alien's two sprites is shown.</summary>
    public int Frame { get; set; }

    /// <summary>Seconds since the last step.</summary>
    public float SinceLastStep { get; set; }

    /// <summary>Number of aliens the current wave started with.</summary>
    public int StartCount { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Starts a new wave of <paramref name="alienCount"/> aliens marching right.</summary>
    public void Reset(int alienCount)
    {
        StartCount = alienCount;
        Direction = 1;
        Frame = 0;
        SinceLastStep = 0f;
    }

    /// <summary>
    /// Linear from <see cref="SlowestStepSeconds"/> (full wave) to <see cref="FastestStepSeconds"/> (one alien left).
    /// </summary>
    public float StepSeconds(int alive)
    {
        if (StartCount <= 1)
            return FastestStepSeconds;

        var share = Math.Clamp((alive - 1) / (float)(StartCount - 1), 0f, 1f);
        return FastestStepSeconds + (SlowestStepSeconds - FastestStepSeconds) * share;
    }
}
