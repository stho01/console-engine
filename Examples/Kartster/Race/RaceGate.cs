using System;
using System.Numerics;

namespace Kartster.Race;

/// <summary>
/// A start/finish line or checkpoint, as Kartster's <c>RaceGatePlane</c>: an upright wall of air across the road
/// through <see cref="Point"/>, facing along the road (<see cref="Forward"/>), reaching <see cref="Left"/> metres to
/// the left and <see cref="Right"/> to the right, from <see cref="Bottom"/> to <see cref="Top"/> (world heights;
/// unbounded until <see cref="StandOn"/> puts it on its road). Planar: x = world X, y = world Z. Karts jump over
/// crests, and a gate may lie under a bridge (dunsfold-neon), so the heights matter.
/// </summary>
public readonly record struct RaceGate(Vector2 Point, Vector2 Forward, float Left, float Right)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's <c>RaceGatePlane</c>: how far over its road a gate reaches with nothing over it.</summary>
    public const float MaxHeightMeters = 60f;

    /// <summary>How far under its road a gate reaches.</summary>
    public const float DepthMeters = 2f;

    /// <summary>The space kept between a gate's top and a road passing over it.</summary>
    public const float HeadroomMeters = 1.5f;

    /// <summary>A road this much over the gate's own is another level (a bridge), not its own road.</summary>
    private const float OtherLevelMeters = 3f;

    private const float SampleMeters = 0.5f; // along the route and across the gate, when standing it on its road

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Kartster's <c>Across</c>: <c>Right</c> of the road, laid flat.</summary>
    public Vector2 Across => new(-Forward.Y, Forward.X);

    /// <summary>Lowest height a kart passes through it at.</summary>
    public float Bottom { get; init; } = float.NegativeInfinity;

    /// <summary>Highest height a kart passes through it at.</summary>
    public float Top { get; init; } = float.PositiveInfinity;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Which way the step from <paramref name="from"/> to <paramref name="to"/> went through the gate: 1 forwards,
    /// -1 backwards, 0 not at all (beside, over or under it). <paramref name="height"/>: the kart's height there. As
    /// Kartster's <c>RaceGatePlane.Cross</c>.
    /// </summary>
    public int Cross(Vector2 from, Vector2 to, float height = 0f)
    {
        var before = Vector2.Dot(from - Point, Forward);
        var after = Vector2.Dot(to - Point, Forward);
        var direction = before < 0f && after >= 0f ? 1
            : before >= 0f && after < 0f ? -1
            : 0;
        if (direction == 0 || height < Bottom || height > Top)
            return 0;

        var hit = Vector2.Lerp(from, to, before / (before - after));
        var lateral = Vector2.Dot(hit - Point, Across);
        return lateral >= -Left && lateral <= Right ? direction : 0;
    }

    /// <summary>
    /// The gate stood on its road of <paramref name="track"/>, as Kartster's <c>RaceGatePlane.Create</c>: its road is
    /// the centerline nearest <see cref="Point"/> running its way (where the road crosses itself, the other level runs
    /// across); from <see cref="DepthMeters"/> under that road to <see cref="MaxHeightMeters"/> over it, or to
    /// <see cref="HeadroomMeters"/> under a road passing over the gate.
    /// </summary>
    public RaceGate StandOn(Track track)
    {
        var road = 0f;
        var nearest = float.MaxValue;
        for (var distance = 0f; distance < track.Length; distance += SampleMeters)
        {
            var (position, forward) = track.Sample(distance);
            if (forward.X * Forward.X + forward.Z * Forward.Y < 0.5f)
                continue;

            var squared = Vector2.DistanceSquared(new Vector2(position.X, position.Z), Point);
            if (squared < nearest)
            {
                nearest = squared;
                road = position.Y;
            }
        }

        var top = road + MaxHeightMeters;
        for (var lateral = -Left; lateral <= Right; lateral += SampleMeters)
        {
            var point = Point + Across * lateral;
            if (track.TryGetRoadHeight(new Vector3(point.X, 0f, point.Y), out var over) && over > road + OtherLevelMeters)
                top = MathF.Min(top, over - HeadroomMeters);
        }

        return this with { Bottom = road - DepthMeters, Top = top };
    }
}
