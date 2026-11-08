using Microsoft.Xna.Framework;

namespace EternalDigger.Components;

public enum MinerMotion : byte
{
    Idle,
    Walking,
    Climbing,
    Falling,
    /// <summary>A hop of <c>MinerControlSystem.JumpTiles</c> tiles sideways over a gap, in an arc.</summary>
    Jumping
}

/// <summary>
/// The player. Occupies one tile (<see cref="Column"/>, <see cref="Row"/>); while moving that is the target tile and
/// the engine's <c>Position</c> (top-left console cell) slides there from <see cref="MoveFrom"/>. Also carries the
/// inventory: gold, ladders and the ore in the bag.
/// </summary>
public struct Miner
{
    // Grid and motion
    public int Column;
    public int Row;

    /// <summary>The tile the current move started from, in tile coordinates.</summary>
    public Vector2 MoveFrom;

    public float MoveElapsed;

    /// <summary>Seconds the current move takes. 0 while idle.</summary>
    public float MoveDuration;

    public MinerMotion Motion;

    /// <summary>Tiles climbed up in a row without letting go of Up. Drives the accelerating climb.</summary>
    public int ClimbStreak;

    /// <summary>Rows fallen in the current fall, for the landing dust.</summary>
    public int FallRows;

    /// <summary>In the lift car, which carries the miner. Up/Down ride; Left/Right (or Down at the bottom) step off.
    /// </summary>
    public bool IsRiding;

    /// <summary>-1 left, 1 right. Which way the sprite looks.</summary>
    public int FacingX;

    /// <summary>Unit vector the headlamp points along (follows the last move or dig direction).</summary>
    public Vector2 LampDirection;

    // Digging
    /// <summary>Seconds until the next shovel hit is allowed.</summary>
    public float DigCooldown;

    /// <summary>Seconds left of the shovel swing animation. 0 when not swinging.</summary>
    public float SwingTime;

    /// <summary>Tile offset of the last dig (-1/0/1 each), for the swing animation.</summary>
    public Point SwingDirection;

    // Rescue
    /// <summary>Seconds the rescue key has been held. Resets when released.</summary>
    public float RescueHold;

    // Dynamite
    /// <summary>Seconds the use key has been held to plant a stick. -1 after planting, until the key is released.</summary>
    public float PlantHold;

    /// <summary>Bought the blasting kit at the shed: sticks can be bought and planted.</summary>
    public bool HasBlastingKit;

    /// <summary>Sticks carried.</summary>
    public int Dynamite;

    /// <summary>Sticks planted whose fuse still burns. A save puts them back into <see cref="Dynamite"/>.</summary>
    public int LitDynamite;

    /// <summary>Lamp oil, 0 to <see cref="LampOil.TankSize"/>. Burns underground only.</summary>
    public float Oil;

    /// <summary>Oil flasks in the pack, poured by themselves when the lamp runs low.</summary>
    public int OilFlasks;

    // Upgrades (0 to Upgrades.MaxTier)
    public int ShovelTier;
    public int LampTier;
    public int BagTier;
    public int DetectorTier;
    public int PickaxeTier;

    // Inventory
    public int Gold;
    public int Ladders;
    public int CopperOre;
    public int SilverOre;
    public int GoldOre;
    public int GemOre;

    /// <summary>Rare finds in the bag. Sold at the door like ore.</summary>
    public int Fossils;
    public int Relics;

    /// <summary>Rare finds ever dug up, kept for the trophy shelf.</summary>
    public int FossilsFound;
    public int RelicsFound;

    public readonly bool IsMoving => MoveDuration > 0f;
    public readonly int OreCount => CopperOre + SilverOre + GoldOre + GemOre + Fossils + Relics;
    public readonly int RareFinds => Fossils + Relics;
}
