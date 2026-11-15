namespace Doom.Play;

public enum WeaponType
{
    Fist,
    Chainsaw,
    Pistol,
    Shotgun,
    Chaingun,
    RocketLauncher,
    PlasmaGun,
    Bfg
}

public enum WeaponKind
{
    Melee,
    Hitscan,
    Missile
}

/// <summary>
/// How a weapon fires and how its view sprite (from the WAD) animates: <see cref="ReadyFrames"/> while idle,
/// <see cref="FireFrames"/> spread over <see cref="RefireTics"/> after a shot, and the muzzle flash sprite.
/// </summary>
public sealed class WeaponInfo
{
    public WeaponType Type { get; init; }
    public string Name { get; init; }

    /// <summary>Number key that selects it.</summary>
    public int Slot { get; init; }

    public WeaponKind Kind { get; init; }
    public AmmoType Ammo { get; init; }
    public int AmmoPerShot { get; init; }

    /// <summary>Tics between shots while the trigger is held.</summary>
    public int RefireTics { get; init; }

    public int Pellets { get; init; } = 1;
    public int DamageMin { get; init; }
    public int DamageMax { get; init; }

    /// <summary>Half the spread of a shot, radians. The first pistol or chaingun shot of a burst goes straight.</summary>
    public float Spread { get; init; }

    public bool FirstShotAccurate { get; init; }
    public float Range { get; init; } = 2048;
    public ThingInfo Missile { get; init; }
    public string Sprite { get; init; }
    public string ReadyFrames { get; init; } = "A";
    public string FireFrames { get; init; }
    public string FlashSprite { get; init; }
    public string FlashFrames { get; init; }
}

/// <summary>Every weapon, indexed by <see cref="WeaponType"/>.</summary>
public static class Weapons
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float MeleeRange = 64;
    private const float BulletSpread = 0.098f;

    public static readonly WeaponInfo[] All =
    [
        new()
        {
            Type = WeaponType.Fist, Name = DoomTexts.WeaponFist, Slot = 1, Kind = WeaponKind.Melee, RefireTics = 18,
            DamageMin = 2, DamageMax = 20, Range = MeleeRange, Sprite = "PUNG", FireFrames = "BCDCB"
        },
        new()
        {
            Type = WeaponType.Chainsaw, Name = DoomTexts.WeaponChainsaw, Slot = 1, Kind = WeaponKind.Melee,
            RefireTics = 4, DamageMin = 2, DamageMax = 20, Range = MeleeRange + 1, Sprite = "SAWG", ReadyFrames = "CD",
            FireFrames = "AB"
        },
        new()
        {
            Type = WeaponType.Pistol, Name = DoomTexts.WeaponPistol, Slot = 2, Kind = WeaponKind.Hitscan,
            Ammo = AmmoType.Bullets, AmmoPerShot = 1, RefireTics = 14, DamageMin = 5, DamageMax = 15,
            Spread = BulletSpread, FirstShotAccurate = true, Sprite = "PISG", FireFrames = "BCDB",
            FlashSprite = "PISF", FlashFrames = "A"
        },
        new()
        {
            Type = WeaponType.Shotgun, Name = DoomTexts.WeaponShotgun, Slot = 3, Kind = WeaponKind.Hitscan,
            Ammo = AmmoType.Shells, AmmoPerShot = 1, RefireTics = 37, Pellets = 7, DamageMin = 5, DamageMax = 15,
            Spread = BulletSpread, Sprite = "SHTG", FireFrames = "ABCDCBA", FlashSprite = "SHTF", FlashFrames = "AB"
        },
        new()
        {
            Type = WeaponType.Chaingun, Name = DoomTexts.WeaponChaingun, Slot = 4, Kind = WeaponKind.Hitscan,
            Ammo = AmmoType.Bullets, AmmoPerShot = 1, RefireTics = 4, DamageMin = 5, DamageMax = 15,
            Spread = BulletSpread, FirstShotAccurate = true, Sprite = "CHGG", FireFrames = "AB",
            FlashSprite = "CHGF", FlashFrames = "AB"
        },
        new()
        {
            Type = WeaponType.RocketLauncher, Name = DoomTexts.WeaponRocketLauncher, Slot = 5,
            Kind = WeaponKind.Missile, Ammo = AmmoType.Rockets, AmmoPerShot = 1, RefireTics = 20,
            Missile = ThingTypes.Rocket, Sprite = "MISG", FireFrames = "B", FlashSprite = "MISF", FlashFrames = "ABCD"
        },
        new()
        {
            Type = WeaponType.PlasmaGun, Name = DoomTexts.WeaponPlasmaGun, Slot = 6, Kind = WeaponKind.Missile,
            Ammo = AmmoType.Cells, AmmoPerShot = 1, RefireTics = 3, Missile = ThingTypes.PlasmaBall, Sprite = "PLSG",
            FireFrames = "B", FlashSprite = "PLSF", FlashFrames = "AB"
        },
        new()
        {
            Type = WeaponType.Bfg, Name = DoomTexts.WeaponBfg, Slot = 7, Kind = WeaponKind.Missile,
            Ammo = AmmoType.Cells, AmmoPerShot = 40, RefireTics = 60, Missile = ThingTypes.BfgBall, Sprite = "BFGG",
            FireFrames = "AB", FlashSprite = "BFGF", FlashFrames = "AB"
        }
    ];

    /// <summary>Most of each ammo type a player can carry without a backpack, indexed by <see cref="AmmoType"/>.</summary>
    public static readonly int[] MaxAmmo = [0, 200, 50, 50, 300];

    /// <summary>The order weapons are tried in when the current one runs dry.</summary>
    public static readonly WeaponType[] Preference =
    [
        WeaponType.PlasmaGun, WeaponType.Chaingun, WeaponType.Shotgun, WeaponType.Pistol, WeaponType.Chainsaw,
        WeaponType.RocketLauncher, WeaponType.Bfg, WeaponType.Fist
    ];

    public static WeaponInfo Get(WeaponType type) => All[(int)type];
}
