namespace EternalDigger.Mining;

/// <summary>One tile of the <see cref="Mine"/>. 5 bytes.</summary>
public struct TileCell
{
    public TileType Type;

    /// <summary>Shovel hits taken so far. The tile breaks when this reaches <see cref="Tiles.HitsToBreak"/>.</summary>
    public byte Damage;

    /// <summary>A placed ladder. Only on <see cref="TileType.Air"/>.</summary>
    public bool HasLadder;

    /// <summary>The headlamp or daylight has lit this tile once. Seen tiles stay faintly visible in the dark.</summary>
    public bool Seen;

    /// <summary>A gas pocket fills this (air) tile: a lit lamp nearby makes it flare (<see cref="GasPockets"/>).</summary>
    public bool HasGas;
}
