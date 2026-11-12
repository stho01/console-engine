using System;

namespace Topple;

/// <summary>What a block is made of. Crystals are the targets: a level is won when none are left.</summary>
public enum Material : byte
{
    Glass,
    Wood,
    Stone,
    Crystal
}

/// <summary>How a material behaves and looks.</summary>
public readonly struct MaterialInfo(float density, float health, float friction, int score,
    ConsoleColor color, ConsoleColor damagedColor)
{
    /// <summary>Mass per cell of area.</summary>
    public float Density { get; } = density;

    /// <summary>Damage it takes before it breaks (see <see cref="Impact"/>).</summary>
    public float Health { get; } = health;

    public float Friction { get; } = friction;

    /// <summary>Points for breaking it.</summary>
    public int Score { get; } = score;

    public ConsoleColor Color { get; } = color;

    /// <summary>The colour once it has lost half its health.</summary>
    public ConsoleColor DamagedColor { get; } = damagedColor;
}

public static class Materials
{
    private static readonly MaterialInfo[] Infos =
    [
        new(density: .5f, health: 4f, friction: .3f, score: 20, ConsoleColor.Cyan, ConsoleColor.DarkCyan),
        new(density: .6f, health: 14f, friction: .7f, score: 50, ConsoleColor.Yellow, ConsoleColor.DarkYellow),
        new(density: 1.6f, health: 32f, friction: .8f, score: 100, ConsoleColor.White, ConsoleColor.Gray),
        new(density: .8f, health: 6f, friction: .5f, score: 500, ConsoleColor.Magenta, ConsoleColor.DarkMagenta)
    ];

    public static ref readonly MaterialInfo Get(Material material) => ref Infos[(int)material];
}
