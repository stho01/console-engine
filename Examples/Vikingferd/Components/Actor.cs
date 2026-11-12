using Microsoft.Xna.Framework;

namespace Vikingferd.Components;

/// <summary>
/// Anything that moves in a level: a hero or a crate. Positions are in tiles (tile (0, 0) at (0, 0), y down), so the
/// rules don't depend on how big a tile is drawn. Moved by <c>PuzzleRules.Step</c> in the fixed step.
/// </summary>
public struct Actor
{
    public ActorKind Kind;

    /// <summary>Top-left corner of the collision box, tiles.</summary>
    public Vector2 Min;

    /// <summary>Size of the collision box, tiles.</summary>
    public Vector2 Size;

    /// <summary>Tiles per second.</summary>
    public Vector2 Velocity;

    /// <summary>Standing on a solid tile, a crate or another hero after the last step.</summary>
    public bool OnGround;

    /// <summary>-1 left, 1 right. Gorm smashes the stone on this side.</summary>
    public int Facing;

    /// <summary>
    /// Bottom edge (y) of the box the last time it stood on something. Landing further below this than
    /// <c>PuzzleRules.FatalDrop</c> without gliding knocks a hero out.
    /// </summary>
    public float LastGroundY;

    /// <summary>Sigrid is falling with her cloak spread. Drawn differently; set by the rules.</summary>
    public bool Gliding;

    //**********************************************************
    //** input (heroes only), latched every frame by PartyControlSystem
    //**********************************************************

    /// <summary>-1, 0 or 1. Always 0 for the heroes the player isn't controlling.</summary>
    public int Run;

    /// <summary>Jump was pressed. Consumed by the next fixed step, jumped or not.</summary>
    public bool JumpRequested;

    /// <summary>Jump is held: Sigrid glides while it is.</summary>
    public bool JumpHeld;

    /// <summary>Use (E) was pressed: pull a lever, or (Gorm) smash cracked stone. Consumed by the next fixed step.</summary>
    public bool UseRequested;
}
