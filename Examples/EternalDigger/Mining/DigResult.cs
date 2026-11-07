namespace EternalDigger.Mining;

public enum DigOutcome : byte
{
    /// <summary>Air, unbreakable or outside the mine. Nothing changed.</summary>
    Nothing,
    Damaged,
    Broken
}

/// <summary>What one <see cref="Mine.Hit"/> did, and to what.</summary>
public readonly struct DigResult(DigOutcome outcome, TileType type, int hits, int hitsToBreak)
{
    public static readonly DigResult Nothing = default;

    public DigOutcome Outcome { get; } = outcome;

    /// <summary>The tile's type before the hit. Use it for debris colour and the ore that drops.</summary>
    public TileType Type { get; } = type;

    public int Hits { get; } = hits;
    public int HitsToBreak { get; } = hitsToBreak;
}
