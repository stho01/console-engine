using System.Runtime.CompilerServices;

namespace ConsoleEngine.Tests.Ecs;

internal struct Position(float x, float y)
{
    public float X = x;
    public float Y = y;
}

internal struct Velocity(float x, float y)
{
    public float X = x;
    public float Y = y;
}

internal struct Health(int value)
{
    public int Value = value;
}

/// <summary>Tag: no fields, so no column.</summary>
internal struct Frozen;

/// <summary>Larger than one chunk.</summary>
[InlineArray(20_000)]
internal struct Huge
{
    private byte _element;
}
