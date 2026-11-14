using System;
using System.Collections.Generic;
using Kartster.Driving;

namespace Kartster.PowerUps;

/// <summary>
/// The tuning the kart model runs on while power-up effects are on a kart, Kartster's <c>KartEffectTuning</c>: the
/// kart's own tuning with engine power times <see cref="KartEffects.EnginePower"/>, both frictions times
/// <see cref="KartEffects.TyreGrip"/> and the surface's grip, the surface's rolling drag, then the surface's speed
/// governor (<c>ApplySurfaceSpeedLimit</c>) at its boosted top-speed share while a speed boost runs. The
/// <c>KartPhysicsSystem</c> asks it per kart per step; null means no effect, the plain surface tuning.
/// </summary>
/// <remarks>
/// The port's <see cref="KartTuning"/> is immutable, so each combination is built once and kept, the multipliers rounded
/// to 0.01 (power) and 0.05 (grip): a diminishing effect walks through about twenty of them.
/// shortcut: a combination's first use allocates its record; pre-build them if that ever shows in a frame.
/// </remarks>
public sealed class KartEffectTuning(PowerUpField field)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float PowerStep = 0.01f;
    private const float GripStep = 0.05f;

    private Dictionary<long, KartTuning>[] _cache;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// The share of top speed a surface allows during a speed boost: Kartster's Circuit palette (gravel 0.75,
    /// grass 0.9), where a boost is what gets a kart across.
    /// </summary>
    public static float GetBoostedTopSpeedFraction(Surface surface) => surface switch
    {
        Surface.Gravel => 0.75f,
        Surface.Grass => 0.9f,
        _ => 1f,
    };

    /// <summary>
    /// The tuning of kart <paramref name="index"/> (model <paramref name="dynamics"/>) on <paramref name="surface"/>
    /// with its effects; null when nothing changes it.
    /// </summary>
    public KartTuning Get(int index, KartDynamics dynamics, Surface surface)
    {
        if (index >= field.KartCount)
            return null;

        var effects = field.Effects(index);
        if (effects.Count == 0)
            return null;

        var power = Math.Max(1, (int)MathF.Round(effects.EnginePower / PowerStep));
        var grip = Math.Max(1, (int)MathF.Round(effects.TyreGrip / GripStep));
        var topSpeed = Math.Clamp((int)MathF.Round(effects.TopSpeed / PowerStep), 1, 100);
        var boosting = effects.IsBoosting;
        if (power == 100 && grip == 20 && topSpeed == 100 && !boosting)
            return null;

        _cache ??= new Dictionary<long, KartTuning>[field.KartCount];
        var cache = _cache[index] ??= new Dictionary<long, KartTuning>();
        var key = ((((long)power * 1024 + grip) * 128 + topSpeed) * 8 + (int)surface) * 2 + (boosting ? 1 : 0);
        if (!cache.TryGetValue(key, out var tuning))
        {
            tuning = Build(dynamics, power * PowerStep, grip * GripStep, surface, boosting, topSpeed * PowerStep);
            cache[key] = tuning;
        }

        return tuning;
    }

    /// <summary>
    /// Kartster's <c>Apply</c> then <c>ApplySurfaceSpeedLimit</c> on the kart's own tuning; the surface's top-speed
    /// share times <paramref name="topSpeedMultiplier"/> (lightning).
    /// </summary>
    public static KartTuning Build(KartDynamics dynamics, float powerMultiplier, float gripMultiplier, Surface surface,
        bool isBoosting, float topSpeedMultiplier = 1f)
    {
        var own = dynamics.Tuning;
        var surfaceGrip = SurfaceHandling.GetGripMultiplier(surface);
        var tuning = own with
        {
            EnginePowerWatts = own.EnginePowerWatts * MathF.Max(powerMultiplier, 0f),
            TyreFriction = own.TyreFriction * MathF.Max(gripMultiplier, 0f) * surfaceGrip,
            TractionFriction = own.TractionFriction * MathF.Max(gripMultiplier, 0f) * surfaceGrip,
            RollingResistance = own.RollingResistance * SurfaceHandling.GetRollingDragMultiplier(surface),
        };

        var fraction = (isBoosting ? GetBoostedTopSpeedFraction(surface) : SurfaceHandling.GetTopSpeedFraction(surface))
                       * MathF.Max(topSpeedMultiplier, 0f);
        if (fraction >= 1f)
            return tuning;

        var governed = KartEngine.GetPowerNeeded(tuning, dynamics.TopSpeed * fraction);
        return tuning with { EnginePowerWatts = MathF.Min(tuning.EnginePowerWatts, governed) };
    }
}
