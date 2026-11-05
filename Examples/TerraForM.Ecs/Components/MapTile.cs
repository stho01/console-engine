namespace TerraForM.Ecs.Components;

/// <summary>The map characters <c>S F H C P B</c>, in that order.</summary>
public enum TileKind : byte
{
    None,
    StartingPoint,
    FinishPoint,
    Rock,
    Crater,
    PlantSpot,
    BonusPoint
}

/// <summary>
/// A map tile at the entity's <c>Position</c>. All kinds share this one component, so all tiles are in one archetype
/// and iterate in creation order (column by column, then the border). Drawing and collision effects depend on that
/// order, as they did in the original.
/// </summary>
public struct MapTile(TileKind kind, int size)
{
    public TileKind Kind = kind;

    /// <summary>Width and height of the collision box, in cells: 10 for a plant spot, 5 for the others.</summary>
    public byte Size = (byte)size;

    /// <summary>Plant spots only: an atmosphere generator was planted here.</summary>
    public bool HasBeenPlanted;

    /// <summary>Bonus points only: the bonus was collected.</summary>
    public bool HasBeenConsumed;
}
