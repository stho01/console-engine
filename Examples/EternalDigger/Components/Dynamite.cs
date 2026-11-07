namespace EternalDigger.Components;

/// <summary>
/// A lit stick of dynamite planted in a tile. Uses the engine's <c>Position</c> (the stick's cell, for effects).
/// Explodes when <see cref="Fuse"/> reaches 0.
/// </summary>
public struct Dynamite(int column, int row, float fuse)
{
    public int Column = column;
    public int Row = row;

    /// <summary>Seconds until the blast.</summary>
    public float Fuse = fuse;

    /// <summary>Seconds until the next fuse spark.</summary>
    public float SparkTimer;
}
