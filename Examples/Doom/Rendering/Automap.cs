using System;
using Doom.Components;
using Doom.Graphics;
using Doom.Maps;
using Doom.Play;

namespace Doom.Rendering;

/// <summary>
/// The map view (TAB): every line seen so far, north up and centred on the player, drawn into the frame. One-sided
/// walls are red, floor steps brown, ceiling steps yellow; with the computer map, unseen lines show grey.
/// </summary>
public sealed class Automap
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float MinZoom = 2;
    public const float MaxZoom = 64;

    /// <summary>The starting <see cref="Zoom"/> for a <see cref="Layout.Standard"/> view.</summary>
    public const float DefaultZoom = 8;

    private readonly byte _wallColor;
    private readonly byte _floorStepColor;
    private readonly byte _ceilingStepColor;
    private readonly byte _unseenColor;
    private readonly byte _playerColor;
    private readonly byte _thingColor;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Automap(Palette palette)
    {
        _wallColor = palette.Closest(252, 0, 0);
        _floorStepColor = palette.Closest(188, 120, 72);
        _ceilingStepColor = palette.Closest(252, 252, 0);
        _unseenColor = palette.Closest(120, 120, 120);
        _playerColor = palette.Closest(255, 255, 255);
        _thingColor = palette.Closest(0, 220, 0);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>World units per pixel.</summary>
    public float Zoom { get; set; } = DefaultZoom;

    /// <summary>Also show things (a cheat; off by default).</summary>
    public bool ShowThings { get; set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Draw(FrameBuffer frame, Level level)
    {
        frame.Clear(0);
        ref var body = ref level.PlayerBody;
        var allMap = level.Player.HasComputerMap;
        foreach (var line in level.Map.Lines)
        {
            if ((line.Flags & LineFlags.NeverOnMap) != 0)
                continue;
            if (!line.Mapped && !allMap && (line.Flags & LineFlags.AlwaysOnMap) == 0)
                continue;

            var color = ColorOf(line);
            if (!line.Mapped && color != 0)
                color = _unseenColor;
            if (color == 0)
                continue;

            DrawWorldLine(frame, body, line.V1.X, line.V1.Y, line.V2.X, line.V2.Y, color);
        }

        if (ShowThings)
        {
            foreach (var chunk in level.Things.GetChunks())
            {
                var things = chunk.GetSpan<Thing>();
                var bodies = chunk.GetSpan<Body>();
                var animations = chunk.GetSpan<Animation>();
                for (var i = 0; i < chunk.Count; i++)
                {
                    if (things[i].Removed || animations[i].Sprite < 0)
                        continue;

                    ref var thing = ref bodies[i];
                    DrawWorldLine(frame, body, thing.X - 4, thing.Y, thing.X + 4, thing.Y, _thingColor);
                }
            }
        }

        DrawArrow(frame, body);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The colour of a line, or 0 for a two-sided line with no height change (not drawn).</summary>
    private byte ColorOf(Line line)
    {
        if (line.Back == null || (line.Flags & LineFlags.Secret) != 0)
            return _wallColor;
        if (line.FrontSector.FloorHeight != line.BackSector.FloorHeight)
            return _floorStepColor;
        if (line.FrontSector.CeilingHeight != line.BackSector.CeilingHeight)
            return _ceilingStepColor;

        return 0;
    }

    private void DrawWorldLine(FrameBuffer frame, in Body center, float x1, float y1, float x2, float y2, byte color)
    {
        var halfWidth = frame.Width / 2f;
        var halfHeight = frame.Height / 2f;
        var sx1 = halfWidth + (x1 - center.X) / Zoom;
        var sy1 = halfHeight - (y1 - center.Y) / Zoom;
        var sx2 = halfWidth + (x2 - center.X) / Zoom;
        var sy2 = halfHeight - (y2 - center.Y) / Zoom;
        if ((sx1 < 0 && sx2 < 0) || (sy1 < 0 && sy2 < 0) || (sx1 >= frame.Width && sx2 >= frame.Width)
            || (sy1 >= frame.Height && sy2 >= frame.Height))
            return;

        frame.DrawLine((int)sx1, (int)sy1, (int)sx2, (int)sy2, color);
    }

    private void DrawArrow(FrameBuffer frame, in Body body)
    {
        var length = 16 * Zoom / 8 * 2;
        var cos = MathF.Cos(body.Angle);
        var sin = MathF.Sin(body.Angle);
        var tipX = body.X + cos * length;
        var tipY = body.Y + sin * length;
        var tailX = body.X - cos * length;
        var tailY = body.Y - sin * length;
        DrawWorldLine(frame, body, tailX, tailY, tipX, tipY, _playerColor);
        for (var side = -1; side <= 1; side += 2)
        {
            var wing = body.Angle + MathF.PI * 0.8f * side;
            DrawWorldLine(frame, body, tipX, tipY, tipX + MathF.Cos(wing) * length * 0.6f,
                tipY + MathF.Sin(wing) * length * 0.6f, _playerColor);
        }
    }
}
