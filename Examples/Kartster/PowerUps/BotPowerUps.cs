using System;
using System.Numerics;
using Kartster.Bots;

namespace Kartster.PowerUps;

/// <summary>
/// When a bot presses use: Kartster's <c>BotPowerUps</c> at its <c>Simple</c> skill (<c>Docs/built-in-bots.md</c>,
/// "Power-ups"). Only with something held, never while the reel spins, at most one press every 0.4 s; anything held
/// for 20 s is used whatever the rules say. Speed boost and engine tune: no corner in the next 60 m. Grip tune: at
/// once. Bananas: a kart 2–25 m behind. Homing Joika: anybody ahead. Lapskaus: a kart 3–80 m ahead within 2.5 m of
/// the heading. Distances to other karts are along the route. No pad seeking or hazard dodging (that would move the
/// bot's line, which is <c>BotDriverSystem</c>'s).
/// </summary>
public sealed class BotPowerUps(int kartCount)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float PressCooldownSeconds = 0.4f;
    public const float MaximumHoldSeconds = 20f;
    public const float StraightMeters = 60f;
    public const float CornerSpeedShare = 0.85f;
    public const float CornerMinimumSpeed = 15f;
    public const float DropForKartBehindMeters = 25f;
    public const float DropForKartBehindFromMeters = 2f;
    public const float CanAheadFromMeters = 3f;
    public const float CanRangeMeters = 80f;
    public const float CanToleranceMeters = 2.5f;

    private readonly float[] _cooldowns = new float[kartCount];
    private readonly float[] _held = new float[kartCount];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Decides for bot <paramref name="self"/>; when it is time, presses use on the <paramref name="field"/>.
    /// </summary>
    /// <param name="route">For "no corner ahead"; null: every stretch counts as straight.</param>
    public void Update(PowerUpField field, ReadOnlySpan<PowerUpKart> karts, int self, BotRoutePlan route,
        float seconds)
    {
        _cooldowns[self] = MathF.Max(0f, _cooldowns[self] - seconds);
        var kind = field.Held(self);
        if (kind == PowerUpKind.None || !field.CanUse || karts.Length < 2)
        {
            _held[self] = 0f;
            return;
        }

        _held[self] += seconds;
        if (_cooldowns[self] > 0f)
            return;

        if (IsGoodMoment(field, karts, self, kind, route) || _held[self] >= MaximumHoldSeconds)
        {
            field.RequestUse(self);
            _cooldowns[self] = PressCooldownSeconds;
            _held[self] = 0f;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static bool IsGoodMoment(PowerUpField field, ReadOnlySpan<PowerUpKart> karts, int self, PowerUpKind kind,
        BotRoutePlan route) => kind switch
    {
        PowerUpKind.SpeedBoost or PowerUpKind.EngineTune => IsStraightAhead(field, karts[self], self, route),
        PowerUpKind.Banana => HasKart(field, karts, self, -DropForKartBehindMeters, -DropForKartBehindFromMeters),
        PowerUpKind.HomingJoika or PowerUpKind.TripleHomingJoika => HasKart(field, karts, self, 1f, float.MaxValue),
        PowerUpKind.Lapskaus or PowerUpKind.TripleLapskaus => IsLinedUp(karts, self),
        _ => true, // grip tune: at once
    };

    /// <summary>The plan's speed stays above 85% of the speed now (at least 15 m/s) for the next 60 m.</summary>
    private static bool IsStraightAhead(PowerUpField field, in PowerUpKart kart, int self, BotRoutePlan route)
    {
        var sample = field.RouteSample(self);
        if (route == null || sample < 0)
            return true;

        var threshold = CornerSpeedShare * MathF.Max(Vector2.Dot(kart.Velocity, kart.Forward), CornerMinimumSpeed);
        for (var ahead = 0f; ahead <= StraightMeters; ahead += route.Spacing * 2f)
        {
            if (route.SpeedAhead(sample, ahead) < threshold)
                return false;
        }

        return true;
    }

    /// <summary>Another kart <paramref name="from"/>..<paramref name="to"/> metres along the route (− behind).</summary>
    private static bool HasKart(PowerUpField field, ReadOnlySpan<PowerUpKart> karts, int self, float from, float to)
    {
        for (var k = 0; k < karts.Length; k++)
        {
            if (k == self)
                continue;

            var gap = field.RouteGap(karts, self, k);
            if (gap >= from && gap <= to)
                return true;
        }

        return false;
    }

    /// <summary>A kart a can fired straight along the heading now would hit.</summary>
    private static bool IsLinedUp(ReadOnlySpan<PowerUpKart> karts, int self)
    {
        var me = karts[self];
        for (var k = 0; k < karts.Length; k++)
        {
            if (k == self)
                continue;

            var offset = karts[k].Position - me.Position;
            var ahead = Vector2.Dot(offset, me.Forward);
            if (ahead > CanAheadFromMeters && ahead <= CanRangeMeters
                && MathF.Abs(Vector2.Dot(offset, me.Right)) < CanToleranceMeters)
                return true;
        }

        return false;
    }
}
