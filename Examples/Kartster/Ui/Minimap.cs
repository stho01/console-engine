using System;
using System.Numerics;

namespace Kartster.Ui;

/// <summary>
/// The whole track, small, in the HUD's top-left corner: the centerline as a white line with a dark drop shadow,
/// a white tick across the start line, other karts as dots in their colours and the player as a yellow arrow on top.
/// North-up and scaled to fit a <see cref="Size"/> × <see cref="Size"/> box, so the whole lap is always in view
/// (Kartster's map instead turns with the player and shows 110 m round them, which a 48-pixel map can't read).
/// The centerline is projected to pixels once, in the constructor; drawing allocates nothing.
/// </summary>
public sealed class Minimap
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int DefaultSize = 48;

    /// <summary>Pixels kept free inside the box, so the arrow and dots at the edge still fit.</summary>
    public const int Padding = 3;

    /// <summary>Half the length of the start tick, pixels.</summary>
    public const int StartTickReach = 2;

    /// <summary>The player's arrow: tip ahead of the kart, back corners behind and to the sides, pixels.</summary>
    private const float ArrowTip = 3f;
    private const float ArrowBack = 2f;
    private const float ArrowSide = 2f;

    private readonly int[] _lineX;
    private readonly int[] _lineY;
    private readonly bool _closed;
    private readonly Vector2 _origin; // the world point at the box's top-left pixel
    private readonly int _startX0, _startY0, _startX1, _startY1;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="centerline">Route points on the ground (x = world X, y = world Z), in driving order.</param>
    /// <param name="closed">A loop: the last point joins the first.</param>
    /// <param name="startPoint">Where the start line crosses the route (ground).</param>
    /// <param name="startForward">The way the route runs through it (ground, unit length).</param>
    /// <param name="left">The box's left edge on the target, pixels.</param>
    /// <param name="top">The box's top edge on the target, pixels.</param>
    /// <param name="size">The box's width and height, pixels.</param>
    public Minimap(ReadOnlySpan<Vector2> centerline, bool closed, Vector2 startPoint, Vector2 startForward,
        int left = Hud.Margin, int top = Hud.Margin, int size = DefaultSize)
    {
        if (centerline.Length < 2)
            throw new ArgumentException("A minimap needs at least two centerline points", nameof(centerline));

        Left = left;
        Top = top;
        Size = size;
        _closed = closed;

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var point in centerline)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        var extent = max - min;
        var inner = size - 1 - 2 * Padding;
        PixelsPerMeter = inner / MathF.Max(MathF.Max(extent.X, extent.Y), 1e-3f);
        // Centred in the box along the shorter side.
        _origin = (min + max) / 2f - new Vector2((size - 1) / 2f / PixelsPerMeter);

        _lineX = new int[centerline.Length];
        _lineY = new int[centerline.Length];
        for (var i = 0; i < centerline.Length; i++)
            (_lineX[i], _lineY[i]) = ToMap(centerline[i]);

        var (x, y) = ToMap(startPoint);
        var across = new Vector2(-startForward.Y, startForward.X);
        _startX0 = x + (int)MathF.Round(across.X * StartTickReach);
        _startY0 = y + (int)MathF.Round(across.Y * StartTickReach);
        _startX1 = x - (int)MathF.Round(across.X * StartTickReach);
        _startY1 = y - (int)MathF.Round(across.Y * StartTickReach);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Left { get; }
    public int Top { get; }
    public int Size { get; }
    public float PixelsPerMeter { get; }

    /// <summary>The centerline's points on the target, pixels.</summary>
    public ReadOnlySpan<int> LineX => _lineX;
    public ReadOnlySpan<int> LineY => _lineY;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// The minimap of <paramref name="track"/>, its centerline sampled every <paramref name="pointCount"/>th of the
    /// route, the start tick at <paramref name="startPoint"/> facing <paramref name="startForward"/> (ground).
    /// </summary>
    public static Minimap FromTrack(Track track, Vector2 startPoint, Vector2 startForward, int pointCount = 128)
    {
        var points = new Vector2[pointCount];
        var step = track.Length / (track.IsClosed ? pointCount : pointCount - 1);
        for (var i = 0; i < pointCount; i++)
        {
            var (position, _) = track.Sample(i * step);
            points[i] = new Vector2(position.X, position.Z);
        }

        return new Minimap(points, track.IsClosed, startPoint, startForward);
    }

    /// <summary>Where a ground point (x = world X, y = world Z) is on the target: north (−Z) up.</summary>
    public (int X, int Y) ToMap(Vector2 ground)
    {
        var pixel = (ground - _origin) * PixelsPerMeter;
        return (Left + (int)MathF.Round(pixel.X), Top + (int)MathF.Round(pixel.Y));
    }

    /// <summary>
    /// Draws the map. <paramref name="positions"/> and <paramref name="colors"/> are every kart's ground position and
    /// dot colour by index; the kart at <paramref name="playerIndex"/> is the arrow, turned by its
    /// <paramref name="playerRadians"/> (the <c>Rotation</c>: 0 faces −Z, positive turns right on the map).
    /// </summary>
    public void Draw(IPixelTarget target, ReadOnlySpan<Vector2> positions, ReadOnlySpan<uint> colors, int playerIndex,
        float playerRadians)
    {
        DrawLine(target, 1, UiColors.TextShadow);
        DrawLine(target, 0, UiColors.Text);
        Shapes.Line(target, _startX0, _startY0, _startX1, _startY1, UiColors.Text);

        for (var i = 0; i < positions.Length; i++)
        {
            if (i == playerIndex)
                continue;

            var (x, y) = ToMap(positions[i]);
            Shapes.FillRect(target, x - 1, y - 1, 3, 3, UiColors.TextShadow);
            target.Set(x, y, colors[i]);
            target.Set(x + 1, y, colors[i]);
            target.Set(x, y + 1, colors[i]);
        }

        if ((uint)playerIndex < (uint)positions.Length)
            DrawArrow(target, positions[playerIndex], playerRadians);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawLine(IPixelTarget target, int offset, uint rgb)
    {
        var count = _closed ? _lineX.Length : _lineX.Length - 1;
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % _lineX.Length;
            Shapes.Line(target, _lineX[i] + offset, _lineY[i] + offset, _lineX[next] + offset, _lineY[next] + offset,
                rgb);
        }
    }

    /// <summary>A filled triangle: lines from the tip to every pixel of the back edge.</summary>
    private void DrawArrow(IPixelTarget target, Vector2 ground, float radians)
    {
        var (x, y) = ToMap(ground);
        var (sin, cos) = MathF.SinCos(radians);
        var forward = new Vector2(sin, -cos); // as KartRenderSystem: world (X, Z) = (sin, -cos), Z down on the map
        var right = new Vector2(cos, sin);
        var tip = Round(forward * ArrowTip);
        var backLeft = Round(-forward * ArrowBack - right * ArrowSide);
        var backRight = Round(-forward * ArrowBack + right * ArrowSide);

        var steps = Math.Max(Math.Abs(backRight.X - backLeft.X), Math.Abs(backRight.Y - backLeft.Y));
        for (var i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0f : i / (float)steps;
            var bx = (int)MathF.Round(backLeft.X + (backRight.X - backLeft.X) * t);
            var by = (int)MathF.Round(backLeft.Y + (backRight.Y - backLeft.Y) * t);
            Shapes.Line(target, x + tip.X, y + tip.Y, x + bx, y + by, UiColors.Selected);
        }
    }

    private static (int X, int Y) Round(Vector2 v) => ((int)MathF.Round(v.X), (int)MathF.Round(v.Y));
}
