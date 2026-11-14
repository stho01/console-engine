using System;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster.Effects;

/// <summary>
/// Dark strips on the road behind the wheels while the kart skids, as Kartster's <c>KartSkidMarks</c>: a skid is the
/// rear axle letting go (from <see cref="MarkingSlip"/> of rear slip), and the front wheels join in from
/// <see cref="FrontMarkingShare"/> of the rear's slip (a spin). Front slip alone is just turning hard and leaves none.
/// Only on asphalt. A fixed ring of segments: the oldest are written over, nothing is allocated while driving.
/// </summary>
public sealed class SkidMarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int MaximumSegments = 1500;
    public const float MarkingSlip = 2f;             // m/s of rear slip
    private const float FrontMarkingShare = 0.75f;
    private const float DarkestSlip = 10f;           // m/s
    private const float DarkestShare = 0.65f;        // Kartster's darkest alpha, mixed into the road colour here
    private const float SegmentMeters = 0.5f;
    private const float LongestSegmentMeters = 5f;   // further than this from the last point: moved, not driven
    private const float LiftMeters = 0.02f;
    private const float WidthMeters = 0.25f;

    private readonly Vector3 _roadColor;
    private readonly Vector3[] _lastPoints = new Vector3[KartWheels.Count];
    private readonly bool[] _marking = new bool[KartWheels.Count];
    private int _next;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="roadColor">The asphalt colour the marks darken.</param>
    public SkidMarks(Vector3 roadColor)
    {
        _roadColor = roadColor;
        var indices = new int[MaximumSegments * 6];
        for (var i = 0; i < MaximumSegments; i++)
        {
            var v = i * 4;
            indices[i * 6] = v;
            indices[i * 6 + 1] = v + 1;
            indices[i * 6 + 2] = v + 2;
            indices[i * 6 + 3] = v;
            indices[i * 6 + 4] = v + 2;
            indices[i * 6 + 5] = v + 3;
        }

        // Every segment starts as a point: zero area, so the rasterizer skips it.
        Mesh = new Mesh(new Vector3[MaximumSegments * 4], indices, new Vector3[MaximumSegments * 2]);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Draw it after the road. Its normals face up (a flat mesh built from points falls back to up).</summary>
    public Mesh Mesh { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Lays marks for this frame from where the wheels are and how much each axle slips.</summary>
    public void Update(in KartWheels wheels, float frontSlip, float rearSlip, bool onAsphalt)
    {
        var rearMarks = onAsphalt && rearSlip >= MarkingSlip;
        var frontMarks = rearMarks && frontSlip >= FrontMarkingShare * rearSlip;

        for (var wheel = 0; wheel < KartWheels.Count; wheel++)
        {
            var isFront = KartWheels.IsFront(wheel);
            if (!(isFront ? frontMarks : rearMarks))
            {
                _marking[wheel] = false;
                continue;
            }

            var point = wheels[wheel] + new Vector3(0f, LiftMeters, 0f);
            if (!_marking[wheel] || Vector3.Distance(point, _lastPoints[wheel]) > LongestSegmentMeters)
            {
                _marking[wheel] = true;
                _lastPoints[wheel] = point;
                continue;
            }

            if (Vector3.Distance(point, _lastPoints[wheel]) < SegmentMeters)
                continue;

            var slip = isFront ? frontSlip : rearSlip;
            var darkness = MathF.Min(slip / DarkestSlip, 1f) * DarkestShare;
            AddSegment(_lastPoints[wheel], point, _roadColor * (1f - darkness));
            _lastPoints[wheel] = point;
        }
    }

    /// <summary>Removes every mark.</summary>
    public void Clear()
    {
        Array.Clear(Mesh.Positions);
        Mesh.MarkChanged();
        Array.Fill(_marking, false);
        _next = 0;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void AddSegment(Vector3 from, Vector3 to, Vector3 color)
    {
        var along = to - from;
        var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, along)) * (WidthMeters / 2f);
        var v = _next * 4;
        var positions = Mesh.Positions;

        // Counter-clockwise seen from above, so it faces up like the road.
        positions[v] = from + side;
        positions[v + 1] = from - side;
        positions[v + 2] = to - side;
        positions[v + 3] = to + side;
        Mesh.TriangleColors[_next * 2] = color;
        Mesh.TriangleColors[_next * 2 + 1] = color;
        Mesh.MarkChanged();
        _next = (_next + 1) % MaximumSegments;
    }
}
