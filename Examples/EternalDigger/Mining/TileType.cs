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
    Bedrock,
    // New types go at the end: saves store the type as this byte.
    /// <summary>The band at <see cref="Tiles.WallDepth"/> the shovel can't break. Only a blast clears it.</summary>
    Obsidian,
    /// <summary>Act 2 rock below the wall, dark blue.</summary>
    Slate,
    /// <summary>Harder act 2 rock below the wall, dark cyan.</summary>
    Quartzite,
    /// <summary>The act 2 ore, below the wall.</summary>
    Gem
}
