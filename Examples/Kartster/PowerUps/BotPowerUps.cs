using System;
using System.Numerics;
using Kartster.Bots;

namespace Kartster.PowerUps;

/// <summary>
/// When a bot presses use: Kartster's <c>BotPowerUps</c> at its <c>Simple</c> skill (<c>Docs/built-in-bots.md</c>,
/// "Power-ups"). Only with something held, never while the reel spins, at most one press every 0.4 s; anything held
/// for 20 s is used whatever the rules say. Speed boost and engine tune: no corner in the next 60 m. Grip tune, fake
/// finish, lightning, earthquake, glitch: at once. Bananas, smoke bomb, fake power-up and roadblocks: a kart 2–25 m
/// behind. Homing Joika and vampire: anybody ahead. Lapskaus: a kart 3–80 m ahead within 2.5 m of the heading.
/// Invulnerable: an armed hazard on the road 0–30 m ahead, or a kart within 8 m. Anvil: a kart within 8 m. Magnet: a
/// kart 3–20 m ahead and none 3–20 m behind. Locomotive: a kart 5–250 m ahead. Distances to other karts are along the
/// route. No pad seeking or hazard dodging (that would move the bot's line, which is <c>BotDriverSystem</c>'s), and a
/// glitched bot does not steer the other way (Kartster's Earl never does).
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
    public const float DangerAheadMeters = 30f;
    public const float BumpMeters = 8f;
    public const float MagnetFromMeters = 3f;
    public const float MagnetToMeters = 20f;
    public const float LocomotiveFromMeters = 5f;
    public const float LocomotiveReachMeters = 250f;

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
        _ when PowerUpCatalog.IsDropped(kind) =>
            HasKart(field, karts, self, -DropForKartBehindMeters, -DropForKartBehindFromMeters),
        PowerUpKind.HomingJoika or PowerUpKind.TripleHomingJoika or PowerUpKind.Vampire =>
            HasKart(field, karts, self, 1f, float.MaxValue),
        PowerUpKind.Lapskaus or PowerUpKind.TripleLapskaus => IsLinedUp(karts, self),
        PowerUpKind.Invulnerable => IsDangerNear(field, karts, self),
        PowerUpKind.Anvil => HasKart(field, karts, self, -BumpMeters, BumpMeters),
        PowerUpKind.Magnet => HasKart(field, karts, self, MagnetFromMeters, MagnetToMeters)
                              && !HasKart(field, karts, self, -MagnetToMeters, -MagnetFromMeters),
        PowerUpKind.Locomotive => HasKart(field, karts, self, LocomotiveFromMeters, LocomotiveReachMeters),
        _ => true, // grip tune, the fake finish, and what strikes everybody (lightning, earthquake, glitch): at once
    };

    /// <summary>An armed hazard on the road 0–30 m ahead, or a kart within 8 m along the route.</summary>
    private static bool IsDangerNear(PowerUpField field, ReadOnlySpan<PowerUpKart> karts, int self)
    {
        var me = karts[self];
        var road = field.Road;
        foreach (ref readonly var hazard in field.Hazards)
        {
            var ahead = Vector2.Dot(hazard.Position - me.Position, me.Forward);
            if (!hazard.IsArmed || ahead < 0f || ahead > DangerAheadMeters)
                continue;

            if (road == null)
                return true;

            var sample = road.Locate(hazard.Position, field.RouteSample(self));
            var (_, lateral) = road.ToRoute(hazard.Position, sample);
            if (lateral >= -road.LeftWidth(sample) && lateral <= road.RightWidth(sample))
                return true;
        }

        return HasKart(field, karts, self, -BumpMeters, BumpMeters);
    }

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
