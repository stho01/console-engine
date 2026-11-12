using System;
using Microsoft.Xna.Framework;
using Vikingferd.Components;

namespace Vikingferd.Puzzle;

/// <summary>What each hero can do, and how to make an <see cref="Actor"/> from a <see cref="Spawn"/>.</summary>
public static class Heroes
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The heroes in switching order (TAB, keys 1-3).</summary>
    public static readonly ActorKind[] Order = [ActorKind.Ylva, ActorKind.Gorm, ActorKind.Sigrid];

    /// <summary>Hero collision box, tiles: fits through one-tile gaps and two-tile-high tunnels.</summary>
    public static readonly Vector2 HeroSize = new(.7f, 1.8f);

    public static readonly Vector2 CrateSize = new(1f, 1f);

    private static readonly HeroStats YlvaStats = new("YLVA", "Jumps three tiles high",
        RunSpeed: 7f, JumpHeight: 3.6f, CanPush: false, CanSmash: false, CanGlide: false);

    private static readonly HeroStats GormStats = new("GORM", "Pushes crates, E smashes cracked stone",
        RunSpeed: 4.5f, JumpHeight: 1.4f, CanPush: true, CanSmash: true, CanGlide: false);

    private static readonly HeroStats SigridStats = new("SIGRID", "Hold jump in the air to glide",
        RunSpeed: 5.5f, JumpHeight: 1.4f, CanPush: false, CanSmash: false, CanGlide: true);

    //**********************************************************
    //** public methods
    //**********************************************************

    public static HeroStats Of(ActorKind kind) => kind switch
    {
        ActorKind.Ylva => YlvaStats,
        ActorKind.Gorm => GormStats,
        ActorKind.Sigrid => SigridStats,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Crates are not heroes.")
    };

    public static bool IsHero(ActorKind kind) => kind != ActorKind.Crate;

    /// <summary>Position in <see cref="Order"/>.</summary>
    public static int IndexOf(ActorKind kind) => (int)kind;

    /// <summary>A hero standing on the bottom of its spawn tile, centred; a crate filling its tile.</summary>
    public static Actor Create(Spawn spawn)
    {
        var size = IsHero(spawn.Kind) ? HeroSize : CrateSize;
        var min = new Vector2(spawn.Column + (1f - size.X) / 2f, spawn.Row + 1f - size.Y);
        return new Actor
        {
            Kind = spawn.Kind,
            Min = min,
            Size = size,
            Facing = 1,
            LastGroundY = min.Y + size.Y
        };
    }
}

/// <summary>A hero's name (as shown), ability text and movement numbers (tiles, seconds).</summary>
public readonly record struct HeroStats(
    string Name,
    string Ability,
    float RunSpeed,
    float JumpHeight,
    bool CanPush,
    bool CanSmash,
    bool CanGlide)
{
    /// <summary>Take-off speed that reaches <see cref="JumpHeight"/> under <see cref="PuzzleRules.Gravity"/>.</summary>
    public float JumpSpeed => MathF.Sqrt(2f * PuzzleRules.Gravity * JumpHeight);
}
