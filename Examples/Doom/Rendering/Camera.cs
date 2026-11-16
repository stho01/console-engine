using System;

namespace Doom.Rendering;

/// <summary>
/// Where the view is: eye position and facing. <see cref="ToView"/> turns a world point into view space: depth
/// along the facing, and side (positive to the right).
/// </summary>
public readonly struct Camera
{
    //**********************************************************
    //** ctor
    //**********************************************************

    public Camera(float x, float y, float z, float angle)
    {
        X = x;
        Y = y;
        Z = z;
        Angle = angle;
        Cos = MathF.Cos(angle);
        Sin = MathF.Sin(angle);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public float X { get; }
    public float Y { get; }

    /// <summary>Eye height.</summary>
    public float Z { get; }

    public float Angle { get; }
    public float Cos { get; }
    public float Sin { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void ToView(float x, float y, out float side, out float depth)
    {
        var dx = x - X;
        var dy = y - Y;
        depth = dx * Cos + dy * Sin;
        side = dx * Sin - dy * Cos;
    }
}
