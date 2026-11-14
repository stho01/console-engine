namespace Starguard;

/// <summary>
/// Screen layout in cells. Rows above <see cref="Top"/> hold the score line, <see cref="Ground"/> is the green line the
/// cannon stands on, and the row below it shows the lives.
/// </summary>
public readonly struct Playfield(int width, int height)
{
    public static readonly Playfield Default = new(96, 52);

    public int Width { get; } = width;
    public int Height { get; } = height;

    /// <summary>First row entities can be in. Shots above it are gone.</summary>
    public int Top => 2;

    /// <summary>The ground line. Shots that reach it are gone.</summary>
    public int Ground => Height - 2;

    /// <summary>Top row of the cannon, which stands on the ground. Aliens reaching it have invaded.</summary>
    public int CannonTop => Ground - 2;
}
