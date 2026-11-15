using System;
using System.Collections.Generic;

namespace Doom.Play;

/// <summary>What a thing is and how it behaves.</summary>
[Flags]
public enum ThingFlags
{
    None = 0,

    /// <summary>Blocks movement.</summary>
    Solid = 1,

    /// <summary>Can be hit and damaged.</summary>
    Shootable = 2,

    /// <summary>Runs the monster AI.</summary>
    Monster = 4,

    /// <summary>Picked up when the player touches it.</summary>
    Pickup = 8,

    /// <summary>Hangs from the ceiling.</summary>
    Hanging = 16,

    /// <summary>Drawn at full brightness (lamps, fireballs, puffs).</summary>
    FullBright = 32,

    /// <summary>Counts for the kill percentage.</summary>
    CountKill = 64,

    /// <summary>Counts for the item percentage.</summary>
    CountItem = 128,

    /// <summary>Never drawn (teleport destinations).</summary>
    Invisible = 256,

    /// <summary>A flying projectile.</summary>
    Missile = 512,

    /// <summary>Drawn see-through (spectre).</summary>
    Shadow = 1024,

    /// <summary>Hits make a puff instead of blood (barrels).</summary>
    NoBlood = 2048,

    /// <summary>Flies at its own height instead of walking on the floor.</summary>
    Float = 4096
}

public enum PickupKind
{
    None,
    Health,
    HealthBonus,
    SoulSphere,
    ArmorBonus,
    Armor,
    Ammo,
    Weapon,
    Key,
    Backpack,
    Berserk,
    Invulnerability,
    RadiationSuit,
    Invisibility,
    ComputerMap,
    LightAmp
}

public enum AmmoType
{
    None,
    Bullets,
    Shells,
    Rockets,
    Cells
}

[Flags]
public enum KeyCards
{
    None = 0,
    Blue = 1,
    Yellow = 2,
    Red = 4
}

/// <summary>
/// What touching a pickup gives. <see cref="Amount"/> is health, armor points or ammo; <see cref="ArmorClass"/> is
/// 1 (absorbs a third) or 2 (absorbs half) for armor.
/// </summary>
public readonly record struct Pickup(
    PickupKind Kind,
    string Message,
    int Amount = 0,
    AmmoType Ammo = AmmoType.None,
    WeaponType Weapon = WeaponType.Fist,
    KeyCards Key = KeyCards.None,
    int ArmorClass = 0);

/// <summary>How a monster attacks.</summary>
public enum MonsterAttack
{
    /// <summary>Instant bullets (<see cref="MonsterInfo.Pellets"/> per shot).</summary>
    Hitscan,

    /// <summary>Claws or bites at close range only.</summary>
    Melee,

    /// <summary>Claws close up, throws <see cref="MonsterInfo.Missile"/> from afar.</summary>
    MeleeOrMissile,

    /// <summary>Flies at the target and bites on contact.</summary>
    Charge
}

/// <summary>The monster part of a <see cref="ThingInfo"/>: speed, attack and animation frame letters.</summary>
public sealed class MonsterInfo
{
    /// <summary>Walking speed, world units per tic.</summary>
    public float Speed { get; init; }

    /// <summary>Chance (out of 256) that a hit makes it flinch.</summary>
    public int PainChance { get; init; }

    public MonsterAttack Attack { get; init; }
    public int Pellets { get; init; } = 1;

    /// <summary>Damage of a bullet, missile or charge.</summary>
    public int DamageMin { get; init; }

    public int DamageMax { get; init; }
    public int MeleeDamageMin { get; init; }
    public int MeleeDamageMax { get; init; }

    /// <summary>The projectile it throws, or null.</summary>
    public ThingInfo Missile { get; init; }

    public string WalkFrames { get; init; } = "ABCD";

    /// <summary>Attack animation; the shot or blow happens on the last frame.</summary>
    public string AttackFrames { get; init; }

    public string PainFrames { get; init; }

    /// <summary>What it drops when killed (a Doom editor number), or 0.</summary>
    public int Drops { get; init; }
}

/// <summary>
/// Everything about one kind of thing: its Doom editor number, sprite, size, flags and behaviour. The frame letters
/// pick pictures from the WAD's sprite of that name; frames the WAD lacks fall back to the sprite's first frame.
/// </summary>
public sealed class ThingInfo
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Shown when a sequence has no letters.</summary>
    private const string FirstFrame = "A";

    private static readonly List<ThingInfo> Registry = [];

    //**********************************************************
    //** ctor
    //**********************************************************

    public ThingInfo()
    {
        lock (Registry)
        {
            Id = Registry.Count;
            Registry.Add(this);
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>A number for this kind, unique in the process: what a <see cref="Components.Thing"/> stores.</summary>
    public int Id { get; }

    public int DoomEdNum { get; init; }

    /// <summary>Four letter sprite name in the WAD, or null for things that are never drawn.</summary>
    public string Sprite { get; init; }

    /// <summary>Frame letters shown while standing (or flying, for projectiles), looped.</summary>
    public string Frames { get; init; } = "A";

    /// <summary>Tics each idle frame shows.</summary>
    public int FrameTics { get; init; } = 10;

    public float Radius { get; init; } = 20;
    public float Height { get; init; } = 16;
    public ThingFlags Flags { get; init; }
    public int Health { get; init; } = 1000;

    /// <summary>Death (or explosion) frames; the last one stays as the corpse unless the thing is removed.</summary>
    public string DeathFrames { get; init; }

    /// <summary>Removed after the death frames (projectiles, explosions) instead of leaving a corpse.</summary>
    public bool RemoveAfterDeath { get; init; }

    /// <summary>The sprite of the death frames when it differs from <see cref="Sprite"/> (a barrel explodes as
    /// <c>BEXP</c>), else null.</summary>
    public string DeathSprite { get; init; }

    /// <summary>A short-lived effect (puff, blood, fog): plays <see cref="DeathFrames"/> once from the start.</summary>
    public bool IsEffect { get; init; }

    /// <summary>Projectiles: speed in world units per tic.</summary>
    public float Speed { get; init; }

    /// <summary>Projectiles: impact damage range.</summary>
    public int DamageMin { get; init; }

    public int DamageMax { get; init; }

    /// <summary>Damage at the centre of an explosion (rockets, barrels); falls off one point per world unit.</summary>
    public int SplashDamage { get; init; }

    public MonsterInfo Monster { get; init; }
    public Pickup Pickup { get; init; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The kind with <see cref="Id"/> <paramref name="id"/>.</summary>
    public static ThingInfo ById(int id) => Registry[id];

    public bool Has(ThingFlags flag) => (Flags & flag) != 0;

    /// <summary>The frame letters of <paramref name="clip"/>; "A" when the kind has none.</summary>
    public string FramesOf(AnimationClip clip)
    {
        var frames = clip switch
        {
            AnimationClip.Walk => Monster?.WalkFrames,
            AnimationClip.Attack => Monster?.AttackFrames,
            AnimationClip.Pain => Monster?.PainFrames,
            AnimationClip.Death => DeathFrames,
            _ => Frames
        };
        return string.IsNullOrEmpty(frames) ? FirstFrame : frames;
    }
}
