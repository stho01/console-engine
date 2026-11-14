namespace Kartster.Bots;

/// <summary>
/// One kart as the bots see it this step, on the <see cref="BotRoad"/>: filled for every kart (bots and player) by
/// the <see cref="BotDriverSystem"/> before any bot decides.
/// </summary>
public struct TrafficKart
{
    /// <summary>Nearest road sample; -1 before the kart is first seen (then the rest means nothing).</summary>
    public int Sample;

    /// <summary>Metres along the route from its start, 0..length.</summary>
    public float Along;

    /// <summary>Metres right of the centerline.</summary>
    public float Lateral;

    /// <summary>Velocity along the route here, m/s (negative = going backwards).</summary>
    public float Speed;

    /// <summary>Route metres driven since first seen, laps included: for rubber-banding.</summary>
    public float Driven;

    /// <summary>The kart a person drives.</summary>
    public bool IsHuman;

    /// <summary>
    /// Invulnerable or heavier than built (<c>KartEffects.IsInvulnerable</c>, <c>Mass</c> &gt; 1, the anvil): never
    /// leaned on, as Kartster's <c>BotBumping.CanBump</c> and <c>IsWorthIt</c>.
    /// </summary>
    public bool IsUnbumpable;
}
