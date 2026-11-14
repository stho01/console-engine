using System;

namespace Kartster.PowerUps;

/// <summary>What a timed effect changes: Kartster's <c>PowerUpEffect</c>s that act on a kart (no view effects).
/// </summary>
public enum EffectChannel
{
    EnginePower,
    TyreGrip,
    Invulnerable,
    SteeringScale,
    SteeringNoise,
    MagnetPull,
    Mass,
    Shrink,
    TopSpeed,
}

/// <summary>
/// The timed effects running on one kart, Kartster's <c>KartEffects</c>: magnitudes, each known by the power-up that
/// started it, its channel and whether it is the user's half of a two-sided one (the vampire's gain). The same
/// power-up again refreshes its effect (new magnitude and full time); different power-ups combine as Kartster's
/// <c>GetModifiers</c>: power, grip, steering scale and mass multiply, steering noise adds, magnet pull is the
/// strongest, shrink and top speed the lowest. A diminishing effect walks back to neutral over its time. Fixed room,
/// no allocation.
/// </summary>
public sealed class KartEffects
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Room for every effect the port's power-ups can stack on one kart.</summary>
    public const int Capacity = 32;

    private readonly Effect[] _effects = new Effect[Capacity];
    private int _count;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Product of every running engine-power effect.</summary>
    public float EnginePower => MathF.Max(Product(EffectChannel.EnginePower), 0f);

    /// <summary>Product of every running tyre-grip effect, at its effective (diminished) magnitude.</summary>
    public float TyreGrip => MathF.Max(Product(EffectChannel.TyreGrip), 0f);

    /// <summary>Glitch: -1 swaps left and right.</summary>
    public float SteeringScale => Product(EffectChannel.SteeringScale);

    /// <summary>Earthquake: how far, as a share of full lock, steering is thrown about each step.</summary>
    public float SteeringNoise => Sum(EffectChannel.SteeringNoise);

    public float MagnetPull => Extreme(EffectChannel.MagnetPull, 0f, strongest: true);

    /// <summary>How much heavier the kart is in a kart contact (anvil); 1: as built.</summary>
    public float Mass => Product(EffectChannel.Mass);

    /// <summary>The size the kart is drawn at (lightning); 1: full size. Only drawn, as in Kartster.</summary>
    public float DrawScale => Extreme(EffectChannel.Shrink, 1f, strongest: false);

    /// <summary>The share of its top speed the kart is held to (lightning); 1: free.</summary>
    public float TopSpeed => Extreme(EffectChannel.TopSpeed, 1f, strongest: false);

    public bool IsInvulnerable => InvulnerableSeconds > 0f;

    /// <summary>Seconds of invulnerability left; 0: none.</summary>
    public float InvulnerableSeconds
    {
        get
        {
            var remaining = 0f;
            for (var i = 0; i < _count; i++)
            {
                if (_effects[i].Channel == EffectChannel.Invulnerable)
                    remaining = MathF.Max(remaining, _effects[i].Remaining);
            }

            return remaining;
        }
    }

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

    /// <param name="selfHalf">The user's half of a two-sided power-up, kept apart from what it does to others.</param>
    public void Add(PowerUpKind source, EffectChannel channel, float magnitude, float seconds, bool diminishes = false,
        bool selfHalf = false)
    {
        var effect = new Effect(source, channel, selfHalf, magnitude, seconds, seconds, diminishes);
        for (var i = 0; i < _count; i++)
        {
            if (_effects[i].Source == source && _effects[i].Channel == channel && _effects[i].SelfHalf == selfHalf)
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
            if (_effects[i].Channel == channel)
                product *= _effects[i].EffectiveMagnitude(1f);
        }

        return product;
    }

    private float Sum(EffectChannel channel)
    {
        var sum = 0f;
        for (var i = 0; i < _count; i++)
        {
            if (_effects[i].Channel == channel)
                sum += _effects[i].EffectiveMagnitude(0f);
        }

        return sum;
    }

    /// <summary>The largest (<paramref name="strongest"/>) or smallest effective magnitude, else the neutral one.</summary>
    private float Extreme(EffectChannel channel, float neutral, bool strongest)
    {
        var value = neutral;
        for (var i = 0; i < _count; i++)
        {
            if (_effects[i].Channel != channel)
                continue;

            var magnitude = _effects[i].EffectiveMagnitude(neutral);
            value = strongest ? MathF.Max(value, magnitude) : MathF.Min(value, magnitude);
        }

        return value;
    }

    private record struct Effect(PowerUpKind Source, EffectChannel Channel, bool SelfHalf, float Magnitude,
        float Duration, float Remaining, bool Diminishes)
    {
        // Kartster's EffectiveMagnitude: a diminishing effect lerps from its magnitude back to neutral.
        public readonly float EffectiveMagnitude(float neutral) => Diminishes
            ? float.Lerp(neutral, Magnitude, Math.Clamp(Remaining / Duration, 0f, 1f))
            : Magnitude;
    }
}
