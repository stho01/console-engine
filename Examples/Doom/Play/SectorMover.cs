using Doom.Maps;

namespace Doom.Play;

public enum MoverKind
{
    /// <summary>Opens, waits, closes.</summary>
    Door,

    /// <summary>Moves once to its target and stops (doors that stay open or shut, floors, ceilings, stairs).</summary>
    OneWay,

    /// <summary>Closes, waits 30 s, opens again.</summary>
    DoorCloseWaitOpen,

    /// <summary>Lowers, waits, rises back.</summary>
    Lift,

    /// <summary>A ceiling that crushes down to 8 above the floor and rises again, for ever.</summary>
    Crusher
}

/// <summary>
/// Moves one sector's floor or ceiling over time. <see cref="Direction"/> is +1 (up), -1 (down) or 0 (waiting).
/// </summary>
public sealed class SectorMover
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Wait times in tics.</summary>
    public const int DoorWait = 150;

    public const int LongDoorWait = 30 * Level.TicRate;
    public const int LiftWait = 105;

    //**********************************************************
    //** props
    //**********************************************************

    public Sector Sector { get; init; }
    public MoverKind Kind { get; init; }

    /// <summary>True when it moves the ceiling, false for the floor.</summary>
    public bool MovesCeiling { get; init; }

    public float Speed { get; init; }

    /// <summary>The height it is heading for now.</summary>
    public float Target { get; set; }

    /// <summary>Doors: the open height. Lifts and crushers: the height they return to.</summary>
    public float Home { get; init; }

    /// <summary>Doors and lifts: the height at the other end (closed door, lowered lift).</summary>
    public float Away { get; init; }

    public int Direction { get; set; }
    public int WaitTics { get; set; }
    public bool Finished { get; set; }

    /// <summary>Damage dealt to things caught under a crusher, every 4 tics.</summary>
    public const int CrushDamage = 10;

    public float Height
    {
        get => MovesCeiling ? Sector.CeilingHeight : Sector.FloorHeight;
        set
        {
            if (MovesCeiling)
                Sector.CeilingHeight = value;
            else
                Sector.FloorHeight = value;
        }
    }
}

public enum LightKind
{
    /// <summary>Sector special 1: dark most of the time, flashes on at random.</summary>
    RandomFlicker,

    /// <summary>Sector specials 2 and 4: half a second dark, bright for a moment.</summary>
    FastStrobe,

    /// <summary>Sector special 3: one second dark, bright for a moment.</summary>
    SlowStrobe,

    /// <summary>Sector special 8: brightens and dims smoothly.</summary>
    Glow,

    /// <summary>Sector special 17: flickers like fire.</summary>
    FireFlicker
}

/// <summary>A sector light that changes on its own between <see cref="Min"/> and <see cref="Max"/>.</summary>
public sealed class LightEffect
{
    public Sector Sector { get; init; }
    public LightKind Kind { get; init; }
    public int Min { get; init; }
    public int Max { get; init; }
    public int Timer { get; set; }
    public int Direction { get; set; } = -1;
}
