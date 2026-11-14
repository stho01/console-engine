using System;

namespace Kartster.PowerUps;

/// <summary>What a timed effect changes: Kartster's handling group (<c>EnginePower</c>, <c>TyreGrip</c>).</summary>
public enum EffectChannel
{
    EnginePower,
    TyreGrip,
}

/// <summary>
/// The timed effects running on one kart, Kartster's <c>KartEffects</c>: multipliers, each known by the power-up that
/// started it and its channel. The same power-up again refreshes its effect (new magnitude and full time); different
/// power-ups multiply. A diminishing effect walks back to 1 over its time. Fixed room, no allocation.
/// </summary>
public sealed class KartEffects
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Room for every effect the port's power-ups can stack on one kart (8 sources × 2 channels).</summary>
    public const int Capacity = 16;

    private readonly Effect[] _effects = new Effect[Capacity];
    private int _count;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Product of every running engine-power effect.</summary>
    public float EnginePower => Product(EffectChannel.EnginePower);

    /// <summary>Product of every running tyre-grip effect, at its effective (diminished) magnitude.</summary>
    public float TyreGrip => Product(EffectChannel.TyreGrip);

    /// <summary>A speed boost is running: the surfaces' boosted top speed applies.</summary>
    public bool IsBoosting
    {
        get
        {
            for (var i = 0; i < _count; i++)
            {
                if (_effects[i].Source == PowerUpKind.SpeedBoost)
                    return true;
            }

            return false;
        }
    }

    public int Count => _count;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(PowerUpKind source, EffectChannel channel, float magnitude, float seconds, bool diminishes = false)
    {
        var effect = new Effect(source, channel, magnitude, seconds, seconds, diminishes);
        for (var i = 0; i < _count; i++)
        {
            if (_effects[i].Source == source && _effects[i].Channel == channel)
            {
                _effects[i] = effect;
                return;
            }
        }

        if (_count < Capacity)
            _effects[_count++] = effect;
    }

    /// <summary>Counts every effect down and drops the ones that ran out.</summary>
    public void Update(float seconds)
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            _effects[i].Remaining -= seconds;
            if (_effects[i].Remaining <= 0f)
                _effects[i] = _effects[--_count];
        }
    }

    public void Clear() => _count = 0;

    //**********************************************************
    //** internals
    //**********************************************************

    private float Product(EffectChannel channel)
    {
        var product = 1f;
        for (var i = 0; i < _count; i++)
        {
            ref readonly var effect = ref _effects[i];
            if (effect.Channel != channel)
                continue;

            // Kartster's EffectiveMagnitude: a diminishing effect lerps from its magnitude back to neutral (1).
            product *= effect.Diminishes
                ? float.Lerp(1f, effect.Magnitude, Math.Clamp(effect.Remaining / effect.Duration, 0f, 1f))
                : effect.Magnitude;
        }

        return MathF.Max(product, 0f);
    }

    private record struct Effect(PowerUpKind Source, EffectChannel Channel, float Magnitude, float Duration,
        float Remaining, bool Diminishes);
}
