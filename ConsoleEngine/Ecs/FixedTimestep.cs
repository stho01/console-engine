using System;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Turns the variable frame time into a whole number of fixed steps ("Fix Your Timestep"). Time that doesn't fill a
/// step is carried over to the next frame. When a frame is so slow that it needs more than
/// <see cref="MaxStepsPerFrame"/> steps, the steps over the cap are dropped instead of carried over, so the game
/// slows down rather than falling further and further behind.
/// </summary>
public sealed class FixedTimestep
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>60 steps per second.</summary>
    public const float DefaultStepSeconds = 1f / 60f;

    public const int DefaultMaxStepsPerFrame = 4;

    private float _accumulator;

    //**********************************************************
    //** ctor
    //**********************************************************

    public FixedTimestep(float stepSeconds = DefaultStepSeconds, int maxStepsPerFrame = DefaultMaxStepsPerFrame)
    {
        if (!float.IsFinite(stepSeconds))
            throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds, "Must be a finite number.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStepsPerFrame);

        StepSeconds = stepSeconds;
        MaxStepsPerFrame = maxStepsPerFrame;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public float StepSeconds { get; }
    public int MaxStepsPerFrame { get; }

    /// <summary>
    /// How far the time is between the last step and the next one, 0 to 1. For interpolating drawn positions.
    /// </summary>
    public float Alpha => _accumulator / StepSeconds;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Adds a frame's time and returns how many steps to run now, 0 to <see cref="MaxStepsPerFrame"/>. A delta that
    /// is not positive (or not a number) adds nothing.
    /// </summary>
    public int Advance(float deltaSeconds)
    {
        if (!(deltaSeconds > 0f))
            return 0;

        _accumulator += deltaSeconds;

        // Compared before dividing, so a huge or infinite delta never reaches the int conversion.
        if (_accumulator >= StepSeconds * (MaxStepsPerFrame + 1))
        {
            _accumulator = 0f;
            return MaxStepsPerFrame;
        }

        var steps = Math.Min((int)(_accumulator / StepSeconds), MaxStepsPerFrame);

        // Rounding can leave the remainder a hair below zero.
        _accumulator = MathF.Max(0f, _accumulator - steps * StepSeconds);
        return steps;
    }

    /// <summary>Drops the time carried over, for example after loading a scene.</summary>
    public void Reset() => _accumulator = 0f;
}
