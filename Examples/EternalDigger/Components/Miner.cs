using Microsoft.Xna.Framework;

namespace EternalDigger.Components;

public enum MinerMotion : byte
{
    Idle,
    Walking,
    Climbing,
    Falling
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

    /// <summary>Rows fallen in the current fall, for the landing dust.</summary>
    public int FallRows;

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

    // Inventory
    public int Gold;
    public int Ladders;
    public int CopperOre;
    public int SilverOre;
    public int GoldOre;

    public readonly bool IsMoving => MoveDuration > 0f;
    public readonly int OreCount => CopperOre + SilverOre + GoldOre;
}
