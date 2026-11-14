using System;
using System.Numerics;
using Kartster.Driving;

namespace Kartster.Bots;

/// <summary>
/// A bot's plan for one track and kart, a simple form of Kartster's <c>BotRoutePlan</c>: for every sample of the
/// <see cref="BotRoad"/> the fastest the bot wants to go there. A corner allows
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

    public const float SampleSpacing = BotRoad.SampleSpacing;
    public const float CurvatureSpan = 10f; // metres either side
    private const float Gravity = 9.81f;

    private readonly float[] _speeds;

    //**********************************************************
    //** ctor
    //**********************************************************

    public BotRoutePlan(Track track, KartTuning tuning, float cornerSpeedFactor, float brakingShare)
        : this(new BotRoad(track), tuning, cornerSpeedFactor, brakingShare) { }

    public BotRoutePlan(BotRoad road, KartTuning tuning, float cornerSpeedFactor, float brakingShare)
    {
        Road = road;
        var count = road.Count;
        _speeds = new float[count];

        var topSpeed = KartEngine.GetTopSpeed(tuning);
        var span = Math.Max(1, (int)MathF.Round(CurvatureSpan / Spacing));
        for (var i = 0; i < count; i++)
        {
            var before = road.ForwardAt(i - span);
            var after = road.ForwardAt(i + span);
            var turn = MathF.Acos(Math.Clamp(Vector2.Dot(before, after), -1f, 1f));
            var curvature = turn / (2 * span * Spacing);
            var cornerSpeed = curvature > 1e-4f
                ? MathF.Sqrt(tuning.TyreFriction * Gravity / curvature) * cornerSpeedFactor
                : float.MaxValue;
            _speeds[i] = MathF.Min(cornerSpeed, topSpeed);
        }

        var deceleration = brakingShare * tuning.BrakeForceNewtons / MathF.Max(tuning.Mass, 0.001f);
        for (var pass = 0; pass < (road.IsClosed ? 2 : 1); pass++)
        for (var i = count - (road.IsClosed ? 1 : 2); i >= 0; i--)
        {
            var next = _speeds[road.Wrap(i + 1)];
            _speeds[i] = MathF.Min(_speeds[i], MathF.Sqrt(next * next + 2 * deceleration * Spacing));
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The road the plan is for, shared by every bot on the track.</summary>
    public BotRoad Road { get; }

    public int Count => Road.Count;

    /// <summary>Metres between samples.</summary>
    public float Spacing => Road.Spacing;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <inheritdoc cref="BotRoad.Locate"/>
    public int Locate(Vector2 position, int hint) => Road.Locate(position, hint);

    /// <inheritdoc cref="BotRoad.PointAhead"/>
    public Vector2 PointAhead(int sample, float metresAhead, float offset) =>
        Road.PointAhead(sample, metresAhead, offset);

    /// <summary>
    /// The planned speed <paramref name="metresAhead"/> along the route from <paramref name="sample"/>, m/s.
    /// </summary>
    public float SpeedAhead(int sample, float metresAhead) =>
        _speeds[Road.Wrap(sample + (int)MathF.Round(metresAhead / Spacing))];
}
