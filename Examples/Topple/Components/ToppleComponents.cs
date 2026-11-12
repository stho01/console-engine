using System;
using Microsoft.Xna.Framework;

namespace Topple.Components;

/// <summary>A breakable body: part of a tower, or a crystal.</summary>
public struct Block(Material material, float health)
{
    public Material Material = material;
    public float Health = health;
    public float MaxHealth = health;

    /// <summary>Set once it has been broken or sunk, so it is scored and destroyed only once.</summary>
    public bool IsWrecked;
}

/// <summary>Tag: a target. The level is won when none are left.</summary>
public struct Crystal;

/// <summary>The body's velocity before the last physics step, for <see cref="Impact.Jolt"/>.</summary>
public struct LastVelocity
{
    public Vector2 Value;
}

public enum ShotKind : byte
{
    Stone,
    Bomb
}

/// <summary>Something the catapult threw.</summary>
public struct Shot(ShotKind kind)
{
    public ShotKind Kind = kind;

    /// <summary>Seconds since it was thrown.</summary>
    public float Age;

    /// <summary>Seconds since the last trail puff.</summary>
    public float SinceTrail;

    /// <summary>Exploded, sunk or timed out: it is being destroyed.</summary>
    public bool IsSpent;
}

/// <summary>A splinter or spark: hits only the ground and disappears after a while.</summary>
public struct Debris(float secondsLeft)
{
    public float SecondsLeft = secondsLeft;
}

/// <summary>Tag: a water sensor. Blocks and shots that fall in sink.</summary>
public struct Water;

/// <summary>
/// A kinematic raft that sways left and right around <see cref="Anchor"/>: x = anchor + amplitude × sin(ωt). The
/// motion is smooth, so what stands on it is carried along by friction instead of being jolted at the turns.
/// </summary>
public struct Ferry(Vector2 anchor, float amplitude, float periodSeconds)
{
    public Vector2 Anchor = anchor;
    public float Amplitude = amplitude;
    public float PeriodSeconds = periodSeconds;
    public float Time;
}

/// <summary>A body drawn filled and textured: its shape in the <c>ShapeLibrary</c> and the material it looks like.</summary>
public struct BlockLook(int shape, Material material)
{
    public int Shape = shape;
    public Material Material = material;
}

public enum PuffKind : byte
{
    /// <summary>Grey trail behind a stone.</summary>
    Smoke,

    /// <summary>A cloud in a material's colour, where a block broke or a shot left the arm.</summary>
    Dust,

    /// <summary>Fire from a bomb's fuse or blast.</summary>
    Flame,

    /// <summary>Spray where something fell into water.</summary>
    Spray
}

/// <summary>A drawn-only particle (no physics): drifts, slows down and fades through its kind's characters.</summary>
public struct Puff(PuffKind kind, Vector2 velocity, float lifeSeconds, ConsoleColor color)
{
    public PuffKind Kind = kind;
    public Vector2 Velocity = velocity;
    public float Age;
    public float Life = lifeSeconds;
    private byte _color = (byte)color;

    public ConsoleColor Color
    {
        readonly get => (ConsoleColor)_color;
        set => _color = (byte)value;
    }
}

/// <summary>An explosion's fireball: a disc that grows to <see cref="Radius"/> and cools from yellow to smoke.</summary>
public struct Burst(float radius, float lifeSeconds)
{
    public float Radius = radius;
    public float Life = lifeSeconds;
    public float Age;
}
