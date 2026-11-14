using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using KartViewer.Driving;

namespace KartViewer.Systems;

/// <summary>
/// Reads the keys each frame into the kart's <see cref="KartControls"/>, which the fixed step reads: W gas, S brake
/// (reverse once stopped), A/D steer, Space handbrake. Pedals are on or off; the steering ramps as Kartster's keyboard
/// steering does (<see cref="RampSteer"/>).
/// </summary>
public sealed class KartInputSystem(Entity kart) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Seconds from centre to full lock.</summary>
    public const float SteerSeconds = 0.25f;

    /// <summary>Seconds from full lock back to centre.</summary>
    public const float ReturnSeconds = 0.12f;

    /// <summary>The most ramp time one frame adds, so a slow frame doesn't jump to full lock.</summary>
    public const float MaxRampSecondsPerFrame = 0.1f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Moves <paramref name="steer"/> towards <paramref name="target"/> (both -1..1) over
    /// <paramref name="seconds"/>: outwards at full lock per <see cref="SteerSeconds"/>, back towards the centre at
    /// full lock per <see cref="ReturnSeconds"/>.
    /// </summary>
    public static float RampSteer(float steer, float target, float seconds)
    {
        // As Kartster's KeyboardKartController.Advance: letting go, or wanting the other side while the wheel is still
        // on this one, moves at the return rate all the way to the target.
        seconds = Math.Clamp(seconds, 0f, MaxRampSecondsPerFrame);
        var returning = target == 0f || steer * target < 0f;
        return MoveTowards(steer, target, seconds / (returning ? ReturnSeconds : SteerSeconds));
    }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        var input = Input.Instance;
        ref var controls = ref World.Get<KartControls>(kart);
        controls.Throttle = input.GetKey(Key.W).Held ? 1f : 0f;
        controls.Brake = input.GetKey(Key.S).Held ? 1f : 0f;
        controls.Handbrake = input.GetKey(Key.SPACE).Held;
        var target = (input.GetKey(Key.D).Held ? 1f : 0f) - (input.GetKey(Key.A).Held ? 1f : 0f);
        controls.Steer = RampSteer(controls.Steer, target, deltaSeconds);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float MoveTowards(float value, float target, float amount) =>
        value < target ? MathF.Min(value + amount, target) : MathF.Max(value - amount, target);
}
