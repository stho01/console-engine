namespace EternalDigger.Mining;

/// <summary>What a tile of the <see cref="Mine"/> is made of. <see cref="Air"/> is dug out (or sky).</summary>
public enum TileType : byte
{
    Air,
    /// <summary>The surface row. Unbreakable, so the shaft is the only way down.</summary>
    Grass,
    Dirt,
    Stone,
    /// <summary>Hard rock that starts at depth <see cref="Tiles.GraniteDepth"/>.</summary>
    Granite,
    Copper,
    Silver,
    Gold,
    /// <summary>The mine's side walls. Unbreakable.</summary>
    Bedrock
}
