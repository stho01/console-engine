using System;
using System.Numerics;
using Kartster.Driving;

namespace Kartster.Bots;

/// <summary>
/// A bot's plan for one track and kart, a simple form of Kartster's <c>BotRoutePlan</c>: the centerline cut into
/// samples about <see cref="SampleSpacing"/> apart, each with the fastest the bot wants to go there. A corner allows
/// <c>sqrt(TyreFriction * g / curvature) * cornerSpeedFactor</c> (curvature over <see cref="CurvatureSpan"/> each
/// way), capped at the kart's top speed; then the speeds are walked backwards twice round so every sample is slow
/// enough to brake down to the next at <c>brakingShare</c> of full braking. Points are in the kart frame
/// (x = world X, y = world Z).
/// </summary>
public sealed class BotRoutePlan
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float SampleSpacing = 2f;
    public const float CurvatureSpan = 10f; // metres either side
    private const float Gravity = 9.81f;
    private const int SearchBehind = 8;
    private const int SearchAhead = 24;
    private const float LostMeters = 15f; // further than this from the sample found near the last: search everywhere

    private readonly Vector2[] _points;
    private readonly Vector2[] _forwards;
    private readonly float[] _speeds;
    private readonly bool _isClosed;

    //**********************************************************
    //** ctor
    //**********************************************************

    public BotRoutePlan(Track track, KartTuning tuning, float cornerSpeedFactor, float brakingShare)
    {
        var count = Math.Max(2, (int)MathF.Round(track.Length / SampleSpacing));
        _isClosed = track.IsClosed;
        Spacing = track.Length / (_isClosed ? count : count - 1);
        _points = new Vector2[count];
        _forwards = new Vector2[count];
        _speeds = new float[count];
        for (var i = 0; i < count; i++)
        {
            var (position, forward) = track.Sample(i * Spacing);
            _points[i] = new Vector2(position.X, position.Z);
            _forwards[i] = Vector2.Normalize(new Vector2(forward.X, forward.Z));
        }

        var topSpeed = KartEngine.GetTopSpeed(tuning);
        var span = Math.Max(1, (int)MathF.Round(CurvatureSpan / Spacing));
        for (var i = 0; i < count; i++)
        {
            var before = _forwards[Wrap(i - span)];
            var after = _forwards[Wrap(i + span)];
            var turn = MathF.Acos(Math.Clamp(Vector2.Dot(before, after), -1f, 1f));
            var curvature = turn / (2 * span * Spacing);
            var cornerSpeed = curvature > 1e-4f
                ? MathF.Sqrt(tuning.TyreFriction * Gravity / curvature) * cornerSpeedFactor
                : float.MaxValue;
            _speeds[i] = MathF.Min(cornerSpeed, topSpeed);
        }

        var deceleration = brakingShare * tuning.BrakeForceNewtons / MathF.Max(tuning.Mass, 0.001f);
        for (var pass = 0; pass < (_isClosed ? 2 : 1); pass++)
        for (var i = count - (_isClosed ? 1 : 2); i >= 0; i--)
        {
            var next = _speeds[Wrap(i + 1)];
            _speeds[i] = MathF.Min(_speeds[i], MathF.Sqrt(next * next + 2 * deceleration * Spacing));
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Count => _points.Length;

    /// <summary>Metres between samples.</summary>
    public float Spacing { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

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
        return point + new Vector2(-forward.Y, forward.X) * offset; // right of forward, as KartBody.Right
    }

    /// <summary>
    /// The planned speed <paramref name="metresAhead"/> along the route from <paramref name="sample"/>, m/s.
    /// </summary>
    public float SpeedAhead(int sample, float metresAhead) =>
        _speeds[Wrap(sample + (int)MathF.Round(metresAhead / Spacing))];

    /// <summary>Samples from <paramref name="from"/> to <paramref name="to"/>, the short way round a closed track.</summary>
    public int StepsBetween(int from, int to)
    {
        var steps = to - from;
        if (!_isClosed)
            return steps;
        var half = _points.Length / 2;
        if (steps > half) steps -= _points.Length;
        else if (steps < -half) steps += _points.Length;
        return steps;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private int Wrap(int index) => _isClosed
        ? ((index % _points.Length) + _points.Length) % _points.Length
        : Math.Clamp(index, 0, _points.Length - 1);

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
