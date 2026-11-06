using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConsoleEngine.Tests.Ecs;

/// <summary>Test-only stand-in for a position. Named apart from <c>ConsoleEngine.Ecs.Components.Position</c>.</summary>
internal struct Location(float x, float y)
{
    public float X = x;
    public float Y = y;
}

internal struct Speed(float x, float y)
{
    public float X = x;
    public float Y = y;
}

internal struct Health(int value)
{
    public int Value = value;
}

internal struct Lifetime(float seconds)
{
    public float Seconds = seconds;
}

internal struct Mass(double value)
{
    public double Value = value;
}

/// <summary>Tag: no fields, so no column.</summary>
internal struct Frozen;

/// <summary>Larger than one chunk.</summary>
[InlineArray(20_000)]
internal struct Huge
{
    private byte _element;
}

/// <summary>No fields but an explicit size: 16 bytes of data, not a tag.</summary>
[StructLayout(LayoutKind.Sequential, Size = 16)]
internal struct Blob;
