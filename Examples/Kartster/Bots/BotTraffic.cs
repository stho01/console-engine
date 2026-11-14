using System;
using System.Numerics;

namespace Kartster.Bots;

/// <summary>
/// How a bot drives among other karts, after Kartster's <c>BotTraffic</c>, <c>BotBumping</c> and
/// <c>BotRubberBand</c> (<c>Docs/built-in-bots.md</c>, "Traffic", "Bumping", "Rubber-banding"). Everything is in
/// route terms (<see cref="TrafficKart"/>: metres along, metres right of the centerline), so it works on any bend.
/// </summary>
public static class BotTraffic
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Centre to centre, side by side, that two karts need, metres (Kartster's 2.2; karts are 1.6 wide).</summary>
    public const float LaneWidth = 2.2f;

    /// <summary>A kart this close along the route, either way, is alongside, metres.</summary>
    public const float AlongsideMeters = 3.5f;

    /// <summary>A kart ahead is in the way within this time gap at the bot's speed...</summary>
    public const float AheadSeconds = 1.2f;

    /// <summary>...but never nearer than this, metres (Kartster: always 4-15 m)...</summary>
    public const float AheadLeastMeters = 8f;

    /// <summary>...nor further than this, metres.</summary>
    public const float AheadMostMeters = 30f;

    /// <summary>Kept between a kart's centre and the road edge, metres (Kartster's edge margin).</summary>
    public const float EdgeMargin = 1.2f;

    /// <summary>A kart ahead closer than this across is in the bot's path: it follows it, metres.</summary>
    public const float PathWidth = 1.8f;

    /// <summary>Centre to centre it keeps behind a kart in its path, metres (2.8 m karts, 0.8 m between).</summary>
    public const float FollowMeters = 3.6f;

    /// <summary>Seconds to close the gap down to <see cref="FollowMeters"/>: the speed limit grows by gap / this.</summary>
    public const float FollowSeconds = 0.5f;

    /// <summary>A kart ahead is worth passing when this much slower than the bot's plan, m/s.</summary>
    public const float PassMargin = 0.5f;

    /// <summary>How fast the aimed-for lane moves across, m/s.</summary>
    public const float LaneChangeSpeed = 3f;

    /// <summary>Centre to centre aimed for when leaning on a kart (under the 1.6 m width, so they touch), metres.</summary>
    public const float LeanMeters = 0.9f;

    /// <summary>How long a lean lasts, seconds (Kartster's on-purpose bump: 1).</summary>
    public const float LeanSeconds = 1f;

    /// <summary>After a lean, or a kart beside it not leaned on, seconds before it thinks about it again.</summary>
    public const float LeanCooldownSeconds = 3f;

    /// <summary>Closer across than this, metres, a kart is in line, not beside: never leaned on.</summary>
    private const float LeanBesideMeters = 1.2f;

    /// <summary>A leaned-on kart this far behind, metres, has been passed: let it go.</summary>
    private const float LeanBehindMeters = 3f;

    /// <summary>A leaned-on kart further ahead than this, metres, is out of reach.</summary>
    private const float LeanAheadMeters = 6f;

    /// <summary>How long a bot goes for a kart that bumped it, seconds (Kartster's <c>BumpBackSeconds</c>).</summary>
    public const float BumpBackSeconds = 1.2f;

    /// <summary>How far past the bumper's line a bump back aims, so it lands, metres (Kartster's push-through).</summary>
    public const float BumpThroughMeters = 0.6f;

    /// <summary>Slower than this, m/s, karts meeting is no bump (Kartster's <c>KartBumpTuning.MinClosingSpeed</c>).</summary>
    public const float MinBumpSpeed = 2f;

    /// <summary>Planned this much under its speed, m/s, the bot is braking for a corner: no leaning then.</summary>
    public const float BrakingMargin = 1f;

    /// <summary>Distance to the leading human at which rubber-banding is full, metres.</summary>
    public const float RubberBandFullMeters = 200f;

    /// <summary>The most rubber-banding changes the pace, as a share.</summary>
    public const float RubberBandShare = 0.1f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>How far ahead a kart in the way is looked for at <paramref name="speed"/>, metres.</summary>
    public static float AheadWindow(float speed) =>
        Math.Clamp(AheadSeconds * speed, AheadLeastMeters, AheadMostMeters);

    /// <summary>
    /// The lane offset the bot in slot <paramref name="self"/> wants given the karts round it, from its
    /// <paramref name="lineOffset"/>, kept in <paramref name="least"/>..<paramref name="most"/>:
    /// <list type="bullet">
    /// <item>Alongside and less than a lane across: at least a lane away, on the side it is on.</item>
    /// <item>Ahead within <see cref="AheadWindow"/>, on its lane and slower than its <paramref name="planSpeed"/>: a lane
    /// to its own side of that kart, or the other side, whichever is on the road and free of other karts.</item>
    /// <item>Any kart ahead in its path now (within <see cref="PathWidth"/> across): <paramref name="speedLimit"/> comes
    /// back no faster than that kart plus the gap beyond <see cref="FollowMeters"/> per <see cref="FollowSeconds"/>,
    /// so it brakes rather than run into it while there is no way past.</item>
    /// </list>
    /// <paramref name="ignore"/> is a kart it neither keeps a lane from nor passes (the one it leans on), or -1; it
    /// still follows that one rather than run into it.
    /// </summary>
    public static float Avoid(BotRoad road, ReadOnlySpan<TrafficKart> karts, int self, int ignore, float lineOffset,
        float planSpeed, float least, float most, ref float speedLimit)
    {
        var me = karts[self];
        var window = AheadWindow(MathF.Max(me.Speed, 0f));
        var offset = lineOffset;
        for (var i = 0; i < karts.Length; i++)
        {
            var other = karts[i];
            if (i == self || other.Sample < 0)
                continue;

            var gap = road.Gap(me.Along, other.Along);
            if (gap > 0f && gap <= window && MathF.Abs(other.Lateral - me.Lateral) < PathWidth)
                speedLimit = MathF.Min(speedLimit,
                    MathF.Max(other.Speed, 0f) + MathF.Max(gap - FollowMeters, 0f) / FollowSeconds);
            if (i == ignore)
                continue;

            var isAlongside = MathF.Abs(gap) < AlongsideMeters;
            var isAhead = gap >= AlongsideMeters && gap <= window && other.Speed < planSpeed - PassMargin;
            if ((!isAlongside && !isAhead) || MathF.Abs(offset - other.Lateral) >= LaneWidth)
                continue;

            var side = me.Lateral >= other.Lateral ? 1f : -1f;
            var mine = other.Lateral + side * LaneWidth;
            if (isAlongside)
            {
                offset = side > 0f ? MathF.Max(offset, mine) : MathF.Min(offset, mine);
                continue;
            }

            var theirs = other.Lateral - side * LaneWidth;
            if (IsFree(road, karts, self, i, mine, least, most, window))
                offset = mine;
            else if (IsFree(road, karts, self, i, theirs, least, most, window))
                offset = theirs;
        }

        return Math.Clamp(offset, least, most);
    }

    /// <summary>
    /// Leaning on a kart beside it, a mild form of Kartster's <c>BotBumping</c> (The Dagg's bumping on purpose): with
    /// a kart from 1 m behind to <see cref="AlongsideMeters"/> ahead, 1.2 m to a lane across (beside, not in line), once per
    /// <see cref="LeanCooldownSeconds"/> the bot holds its line onto it with chance <paramref name="aggression"/>, aiming
    /// <see cref="LeanMeters"/> from its centre for <see cref="LeanSeconds"/>, so the karts' contact pushes. Never while
    /// <paramref name="isBraking"/> for a corner, never past <paramref name="least"/>..<paramref name="most"/> (the
    /// edge margin), and it lets go once that kart is 3 m behind or 6 m ahead. Returns <paramref name="offset"/> when it
    /// is leaning on nobody. A kart that <see cref="TrafficKart.IsUnbumpable"/> is never picked, and a lean or bump
    /// back on one ends.
    /// </summary>
    public static float Lean(BotRoad road, ReadOnlySpan<TrafficKart> karts, int self, ref BotTrafficState state,
        float aggression, bool isBraking, float offset, float least, float most, float seconds)
    {
        var me = karts[self];
        state.LeanCooldownSeconds = MathF.Max(0f, state.LeanCooldownSeconds - seconds);
        if (state.LeanTarget >= 0)
        {
            state.LeanSeconds -= seconds;
            var target = karts[state.LeanTarget];
            var gap = road.Gap(me.Along, target.Along);
            if (isBraking || state.LeanSeconds <= 0f || gap < -LeanBehindMeters || gap > LeanAheadMeters ||
                target.IsUnbumpable)
            {
                StopLean(ref state);
                return offset;
            }

            var side = target.Lateral >= me.Lateral ? 1f : -1f;
            var aim = state.IsBumpingBack
                ? target.Lateral + side * BumpThroughMeters
                : target.Lateral - side * LeanMeters;
            return Math.Clamp(aim, least, most);
        }

        if (isBraking || state.LeanCooldownSeconds > 0f || aggression <= 0f)
            return offset;

        for (var i = 0; i < karts.Length; i++)
        {
            var other = karts[i];
            if (i == self || other.Sample < 0 || other.IsUnbumpable)
                continue;

            var gap = road.Gap(me.Along, other.Along);
            var across = MathF.Abs(other.Lateral - me.Lateral);
            if (gap < -1f || gap > AlongsideMeters || across >= LaneWidth || across < LeanBesideMeters)
                continue;

            state.LeanCooldownSeconds = LeanCooldownSeconds; // one roll per kart met
            if (state.NextUnit() < aggression)
            {
                state.LeanTarget = i;
                state.LeanSeconds = LeanSeconds;
            }

            return offset;
        }

        return offset;
    }

    /// <summary>
    /// Kartster's <c>BotBumping</c> bump back (Merlin, Stag and The Dagg; <see cref="BotDriverSystem.BumpsBack"/>):
    /// the bot was driven into by kart slot <paramref name="bumper"/>, so unless it is already going for a kart, or
    /// <paramref name="bumper"/> is the one it last went for and the cooldown runs, it goes for it for
    /// <see cref="BumpBackSeconds"/>: <see cref="Lean"/> aims <see cref="BumpThroughMeters"/> past the bumper's line,
    /// with the lean's limits (not while braking for a corner, inside the edge margin, 3 m behind to 6 m ahead).
    /// </summary>
    public static void BumpedBy(ref BotTrafficState state, int bumper)
    {
        if (state.LeanTarget >= 0 || (state.LeanCooldownSeconds > 0f && state.LastLeanTarget == bumper))
            return;

        state.LeanTarget = bumper;
        state.LeanSeconds = BumpBackSeconds;
        state.IsBumpingBack = true;
    }

    /// <summary>The bot drove into kart slot <paramref name="other"/>: a bump back at that kart has landed and ends.</summary>
    public static void Bumped(ref BotTrafficState state, int other)
    {
        if (state.IsBumpingBack && state.LeanTarget == other)
            StopLean(ref state);
    }

    /// <summary>
    /// Whether kart A drove into kart B, given both karts' positions and velocities before they met, or B into A; false
    /// in <paramref name="isBump"/> when they met slower than <see cref="MinBumpSpeed"/>. As Kartster: along the line
    /// between them, whoever brought more of the closing speed is the bumper.
    /// </summary>
    public static bool AWasBumper(Vector2 positionA, Vector2 velocityA, Vector2 positionB, Vector2 velocityB,
        out bool isBump)
    {
        var normal = Vector2.Normalize(positionB - positionA);
        var closing = -Vector2.Dot(velocityB - velocityA, normal);
        isBump = closing >= MinBumpSpeed;
        return Vector2.Dot(velocityA, normal) >= -Vector2.Dot(velocityB, normal);
    }

    /// <summary>
    /// Kartster's optional catch-up: what to multiply the pace of the bot in slot <paramref name="self"/> by. Below 1
    /// ahead of the leading human, above 1 behind, <see cref="RubberBandShare"/> at most from
    /// <see cref="RubberBandFullMeters"/> away; 1 without a human.
    /// </summary>
    public static float RubberBand(ReadOnlySpan<TrafficKart> karts, int self)
    {
        var human = float.NegativeInfinity;
        for (var i = 0; i < karts.Length; i++)
            if (karts[i].IsHuman && karts[i].Sample >= 0)
                human = MathF.Max(human, karts[i].Driven);
        if (float.IsNegativeInfinity(human))
            return 1f;

        var ahead = karts[self].Driven - human;
        return 1f - Math.Clamp(ahead / RubberBandFullMeters, -1f, 1f) * RubberBandShare;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void StopLean(ref BotTrafficState state)
    {
        state.LastLeanTarget = state.LeanTarget;
        state.LeanTarget = -1;
        state.IsBumpingBack = false;
        state.LeanCooldownSeconds = LeanCooldownSeconds;
    }

    /// <summary>
    /// <paramref name="lane"/> is on the road and no kart but <paramref name="passing"/> is alongside or ahead in the
    /// window on it.
    /// </summary>
    private static bool IsFree(BotRoad road, ReadOnlySpan<TrafficKart> karts, int self, int passing, float lane,
        float least, float most, float window)
    {
        if (lane < least || lane > most)
            return false;

        var me = karts[self];
        for (var i = 0; i < karts.Length; i++)
        {
            if (i == self || i == passing || karts[i].Sample < 0)
                continue;

            var gap = road.Gap(me.Along, karts[i].Along);
            if (gap > -AlongsideMeters && gap <= window && MathF.Abs(karts[i].Lateral - lane) < LaneWidth)
                return false;
        }

        return true;
    }
}
