using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Race;

namespace Kartster.Systems;

/// <summary>
/// Reads the keys each frame into the <see cref="PlayerKart"/>'s <see cref="KartControls"/> (bots, and a player on
/// autopilot with a <see cref="Bot"/>, have the <c>BotDriverSystem</c>), which the fixed step reads: W gas, S brake
/// (reverse once stopped), A/D steer, Space handbrake. Pedals are on or off; the steering ramps as Kartster's keyboard
/// steering does (<see cref="RampSteer"/>). While the <paramref name="race"/> can't drive (the countdown) the controls
/// are held at zero, the wheel straight, as Kartster zeroes every driving input.
/// </summary>
/// <param name="isHeld">Whether a key is held; null reads <c>Input.Instance</c> (tests pass their own).</param>
/// <param name="race">Null: always drives.</param>
public sealed class KartInputSystem(Func<Key, bool> isHeld = null, RaceSession race = null) : EntitySystem
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

    private readonly Func<Key, bool> _isHeld = isHeld ?? (key => Input.Instance.GetKey(key).Held);
    private Query _players;

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

    protected override void OnCreate() => _players = World.Query(Query.With<PlayerKart, KartControls>().Without<Bot>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (race is { CanDrive: false })
        {
            foreach (var chunk in _players.GetChunks())
                chunk.GetSpan<KartControls>()[..chunk.Count].Clear();
            return;
        }

        var throttle = _isHeld(Key.W) ? 1f : 0f;
        var brake = _isHeld(Key.S) ? 1f : 0f;
        var handbrake = _isHeld(Key.SPACE);
        var target = (_isHeld(Key.D) ? 1f : 0f) - (_isHeld(Key.A) ? 1f : 0f);
        foreach (var chunk in _players.GetChunks())
        {
            var controls = chunk.GetSpan<KartControls>();
            for (var i = 0; i < chunk.Count; i++)
            {
                controls[i].Throttle = throttle;
                controls[i].Brake = brake;
                controls[i].Handbrake = handbrake;
                controls[i].Steer = RampSteer(controls[i].Steer, target, deltaSeconds);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float MoveTowards(float value, float target, float amount) =>
        value < target ? MathF.Min(value + amount, target) : MathF.Max(value - amount, target);
}
