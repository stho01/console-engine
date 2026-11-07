namespace EternalDigger.Mining;

/// <summary>A tile that differs from what the seed generates, as it is saved: where it is and what it is now.</summary>
public readonly record struct TileChange(int Column, int Row, TileCell Cell);
