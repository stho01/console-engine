using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks;

public struct Position
{
    public Vector2 Value;
}

public struct Velocity
{
    public Vector2 Value;
}

/// <summary>Tag component: no fields, so it has no column in a chunk.</summary>
public struct Frozen;
