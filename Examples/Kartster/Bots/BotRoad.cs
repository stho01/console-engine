using System;
using System.Numerics;

namespace Kartster.Bots;

/// <summary>
/// The road as the bots see it, made once per track: the centerline cut into samples about
/// <see cref="SampleSpacing"/> apart, each with its direction and how far the asphalt reaches either side (measured on
/// the ribbon with <see cref="Track.TryGetRoadHeight"/>). Kartster's <c>BotRoad</c> and route navigator in one. Points
/// are in the kart frame (x = world X, y = world Z).
/// </summary>
public sealed class BotRoad
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float SampleSpacing = 2f;
    private const int SearchBehind = 8;
    private const int SearchAhead = 24;
    private const float LostMeters = 15f; // further than this from the sample found near the last: search everywhere
    private const float WidthProbeStep = 0.5f;
    private const float WidthProbeMost = 15f;
    private const int WidthRefinements = 3; // halvings of the last probe step: 6 cm
    private const float WidthGuessTolerance = 0.1f;

    private readonly Vector2[] _points;
    private readonly Vector2[] _forwards;
    private readonly float[] _leftWidths;
    private readonly float[] _rightWidths;

    //**********************************************************
    //** ctor
    //**********************************************************

    public BotRoad(Track track)
    {
        var count = Math.Max(2, (int)MathF.Round(track.Length / SampleSpacing));
        IsClosed = track.IsClosed;
        Length = track.Length;
        Spacing = track.Length / (IsClosed ? count : count - 1);
        _points = new Vector2[count];
        _forwards = new Vector2[count];
        _leftWidths = new float[count];
        _rightWidths = new float[count];
        for (var i = 0; i < count; i++)
        {
            var (position, forward) = track.Sample(i * Spacing);
            _points[i] = new Vector2(position.X, position.Z);
            _forwards[i] = Vector2.Normalize(new Vector2(forward.X, forward.Z));
            var right = RightOf(_forwards[i]);
            _rightWidths[i] = MeasureWidth(track, _points[i], right, i > 0 ? _rightWidths[i - 1] : 0f);
            _leftWidths[i] = MeasureWidth(track, _points[i], -right, i > 0 ? _leftWidths[i - 1] : 0f);
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Count => _points.Length;

    /// <summary>Metres between samples.</summary>
    public float Spacing { get; }

    /// <summary>Metres round the route.</summary>
    public float Length { get; }

    public bool IsClosed { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public Vector2 PointAt(int sample) => _points[Wrap(sample)];

    public Vector2 ForwardAt(int sample) => _forwards[Wrap(sample)];

    /// <summary>Asphalt left of the centerline at <paramref name="sample"/>, metres.</summary>
    public float LeftWidth(int sample) => _leftWidths[Wrap(sample)];

    /// <summary>Asphalt right of the centerline at <paramref name="sample"/>, metres.</summary>
    public float RightWidth(int sample) => _rightWidths[Wrap(sample)];

    /// <summary>
    /// The narrowest asphalt either side over <paramref name="samples"/> samples from <paramref name="sample"/> on.
    /// </summary>
    public (float Left, float Right) NarrowestAhead(int sample, int samples)
    {
        var left = float.MaxValue;
        var right = float.MaxValue;
        for (var i = 0; i <= samples; i++)
        {
            left = MathF.Min(left, LeftWidth(sample + i));
            right = MathF.Min(right, RightWidth(sample + i));
        }

        return (left, right);
    }

    /// <summary>
    /// The sample nearest <paramref name="position"/>, looked for around <paramref name="hint"/> (the last one found)
    /// and everywhere when <paramref name="hint"/> is negative or the kart is far from the road there.
    /// </summary>
    public int Locate(Vector2 position, int hint)
    {
        if (hint >= 0)
        {
            var best = Nearest(position, hint - SearchBehind, hint + SearchAhead);
            if (Vector2.DistanceSquared(_points[best], position) <= LostMeters * LostMeters)
                return best;
        }

        return Nearest(position, 0, _points.Length - 1);
    }

    /// <summary>
    /// Where <paramref name="position"/> is, seen from <paramref name="sample"/> (its <see cref="Locate"/>d one):
    /// metres along the route from its start and metres right of the centerline.
    /// </summary>
    public (float Along, float Lateral) ToRoute(Vector2 position, int sample)
    {
        var offset = position - _points[sample];
        var forward = _forwards[sample];
        return (sample * Spacing + Vector2.Dot(offset, forward), Vector2.Dot(offset, RightOf(forward)));
    }

    /// <summary>
    /// <paramref name="to"/> minus <paramref name="from"/>, route metres: on a closed track the nearer way round
    /// (negative = behind).
    /// </summary>
    public float Gap(float from, float to)
    {
        var gap = to - from;
        if (!IsClosed)
            return gap;

        var half = Length / 2f;
        return gap > half ? gap - Length : gap < -half ? gap + Length : gap;
    }

    /// <summary>
    /// The point <paramref name="metresAhead"/> along the route from <paramref name="sample"/>,
    /// <paramref name="offset"/> metres right of the centerline.
    /// </summary>
    public Vector2 PointAhead(int sample, float metresAhead, float offset)
    {
        var steps = metresAhead / Spacing;
        var whole = (int)MathF.Floor(steps);
        var a = Wrap(sample + whole);
        var b = Wrap(sample + whole + 1);
        var share = steps - whole;
        var point = Vector2.Lerp(_points[a], _points[b], share);
        var forward = Vector2.Lerp(_forwards[a], _forwards[b], share);
        return point + RightOf(forward) * offset;
    }

    public int Wrap(int index) => IsClosed
        ? ((index % _points.Length) + _points.Length) % _points.Length
        : Math.Clamp(index, 0, _points.Length - 1);

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Right of <paramref name="forward"/>, as <c>KartBody.Right</c>.</summary>
    private static Vector2 RightOf(Vector2 forward) => new(-forward.Y, forward.X);

    /// <summary>
    /// The edge within <see cref="WidthGuessTolerance"/> of <paramref name="guess"/> (the last sample's), which two
    /// probes confirm on most of a road; otherwise steps out from the centerline until off the asphalt, then narrows
    /// the edge down. Each probe tests every road triangle, so this keeps a track's road to some tens of milliseconds.
    /// </summary>
    private static float MeasureWidth(Track track, Vector2 point, Vector2 direction, float guess)
    {
        if (guess > WidthGuessTolerance &&
            IsOnRoad(track, point + direction * (guess - WidthGuessTolerance)) &&
            !IsOnRoad(track, point + direction * (guess + WidthGuessTolerance)))
            return guess;

        var inside = 0f;
        var outside = WidthProbeMost;
        for (var distance = WidthProbeStep; distance <= WidthProbeMost; distance += WidthProbeStep)
        {
            if (!IsOnRoad(track, point + direction * distance))
            {
                outside = distance;
                break;
            }

            inside = distance;
        }

        for (var i = 0; i < WidthRefinements && outside - inside > 0.01f; i++)
        {
            var middle = (inside + outside) / 2f;
            if (IsOnRoad(track, point + direction * middle))
                inside = middle;
            else
                outside = middle;
        }

        return inside;
    }

    private static bool IsOnRoad(Track track, Vector2 point) =>
        track.TryGetRoadHeight(new Vector3(point.X, 0f, point.Y), out _);

    private int Nearest(Vector2 position, int from, int to)
    {
        var best = Wrap(from);
        var bestDistance = float.MaxValue;
        for (var i = from; i <= to; i++)
        {
            var index = Wrap(i);
            var distance = Vector2.DistanceSquared(_points[index], position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = index;
            }
        }

        return best;
    }
}
