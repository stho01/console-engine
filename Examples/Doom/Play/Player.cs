using System;
using ConsoleEngine.Ecs;

namespace Doom.Play;

/// <summary>What the player carries from one level to the next. Keys and power-ups do not carry over.</summary>
public sealed class PlayerLoadout
{
    public int Health { get; init; } = Player.StartHealth;
    public int Armor { get; init; }
    public int ArmorClass { get; init; }
    public bool[] Weapons { get; init; } = StartWeapons();
    public int[] Ammo { get; init; } = [0, 50, 0, 0, 0];
    public bool Backpack { get; init; }
    public WeaponType Weapon { get; init; } = WeaponType.Pistol;

    /// <summary>Fist and pistol, the pistol-start loadout.</summary>
    public static bool[] StartWeapons()
    {
        var weapons = new bool[Play.Weapons.All.Length];
        weapons[(int)WeaponType.Fist] = true;
        weapons[(int)WeaponType.Pistol] = true;
        return weapons;
    }
}

/// <summary>
/// The player: health, armor, inventory, power-ups and the weapon state. Position and collision live in
/// <see cref="Body"/>, an invisible thing in the level's world that monsters target.
/// </summary>
public sealed class Player
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int StartHealth = 100;
    public const int MaxHealth = 100;
    public const int MaxBonusHealth = 200;
    public const int MaxArmor = 200;

    /// <summary>Eye height above the feet.</summary>
    public const float ViewHeight = 41;

    /// <summary>Tics a power-up lasts.</summary>
    public const int InvulnerabilityTics = 30 * Level.TicRate;

    public const int RadiationSuitTics = 60 * Level.TicRate;
    public const int InvisibilityTics = 60 * Level.TicRate;
    public const int LightAmpTics = 120 * Level.TicRate;

    /// <summary>How far down the weapon starts when it is drawn, in view pixels at 320 × 200.</summary>
    public const int WeaponLowered = 96;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Player(Entity body, PlayerLoadout loadout, float z)
    {
        Body = body;
        Health = loadout.Health;
        Armor = loadout.Armor;
        ArmorClass = loadout.ArmorClass;
        Array.Copy(loadout.Weapons, OwnedWeapons, Math.Min(loadout.Weapons.Length, OwnedWeapons.Length));
        Array.Copy(loadout.Ammo, Ammo, Math.Min(loadout.Ammo.Length, Ammo.Length));
        Backpack = loadout.Backpack;
        Weapon = OwnedWeapons[(int)loadout.Weapon] ? loadout.Weapon : WeaponType.Pistol;
        PendingWeapon = Weapon;
        ViewZ = z + ViewHeight;
        CurrentViewHeight = ViewHeight;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The player's thing: it has a <see cref="Components.Thing"/>, <see cref="Components.Body"/> and
    /// <see cref="Components.Momentum"/>, but no <see cref="Components.Animation"/> (it is never drawn).</summary>
    public Entity Body { get; }
    public int Health { get; set; }
    public int Armor { get; set; }

    /// <summary>0 none, 1 absorbs a third of the damage, 2 absorbs half.</summary>
    public int ArmorClass { get; set; }

    public bool[] OwnedWeapons { get; } = new bool[Weapons.All.Length];
    public int[] Ammo { get; } = new int[Weapons.MaxAmmo.Length];
    public bool Backpack { get; set; }
    public KeyCards Keys { get; set; }
    public WeaponType Weapon { get; set; }
    public WeaponType PendingWeapon { get; set; }
    public bool Dead => Health <= 0;

    /// <summary>Tics until the weapon can fire again.</summary>
    public int RefireTics { get; set; }

    /// <summary>Tics left of the fire animation, and of the muzzle flash.</summary>
    public int FireTics { get; set; }

    public int FlashTics { get; set; }

    /// <summary>The trigger was held on the previous shot (pistol and chaingun lose accuracy).</summary>
    public bool Refiring { get; set; }

    /// <summary>How far the weapon is lowered: 0 ready, <see cref="WeaponLowered"/> out of sight.</summary>
    public int WeaponOffset { get; set; }

    /// <summary>Eye height in the world, with step smoothing and bobbing.</summary>
    public float ViewZ { get; set; }

    public float CurrentViewHeight { get; set; }
    public float Bob { get; set; }
    public float BobPhase { get; set; }

    /// <summary>Red and gold screen tints: they fade one step per tic.</summary>
    public int DamageCount { get; set; }

    public int BonusCount { get; set; }
    public int InvulnerabilityLeft { get; set; }
    public int RadiationSuitLeft { get; set; }
    public int InvisibilityLeft { get; set; }
    public int LightAmpLeft { get; set; }
    public bool Berserk { get; set; }

    /// <summary>Tics since berserk was picked up, for its fading red tint.</summary>
    public int BerserkFade { get; set; } = int.MaxValue;

    public bool HasComputerMap { get; set; }

    /// <summary>Run speed (on by default; R toggles walking).</summary>
    public bool Running { get; set; } = true;

    //**********************************************************
    //** public methods
    //**********************************************************

    public int MaxAmmoOf(AmmoType type) => Weapons.MaxAmmo[(int)type] * (Backpack ? 2 : 1);

    public bool HasAmmoFor(WeaponType type)
    {
        var info = Weapons.Get(type);
        return info.Ammo == AmmoType.None || Ammo[(int)info.Ammo] >= info.AmmoPerShot;
    }

    /// <summary>
    /// Applies a pickup. Returns false (and the pickup stays where it is) when the player can't use it now.
    /// <paramref name="dropped"/> ammo, from a dead monster, gives half.
    /// </summary>
    public bool Give(in Pickup pickup, bool dropped)
    {
        switch (pickup.Kind)
        {
            case PickupKind.Health:
                if (Health >= MaxHealth)
                    return false;
                Health = Math.Min(MaxHealth, Health + pickup.Amount);
                return true;
            case PickupKind.HealthBonus:
            case PickupKind.SoulSphere:
                Health = Math.Min(MaxBonusHealth, Health + pickup.Amount);
                return true;
            case PickupKind.ArmorBonus:
                Armor = Math.Min(MaxArmor, Armor + pickup.Amount);
                if (ArmorClass == 0)
                    ArmorClass = 1;
                return true;
            case PickupKind.Armor:
                if (Armor >= pickup.Amount)
                    return false;
                Armor = pickup.Amount;
                ArmorClass = pickup.ArmorClass;
                return true;
            case PickupKind.Ammo:
                return GiveAmmo(pickup.Ammo, dropped ? Math.Max(1, pickup.Amount / 2) : pickup.Amount);
            case PickupKind.Backpack:
                Backpack = true;
                GiveAmmo(AmmoType.Bullets, 10);
                GiveAmmo(AmmoType.Shells, 4);
                GiveAmmo(AmmoType.Rockets, 1);
                GiveAmmo(AmmoType.Cells, 20);
                return true;
            case PickupKind.Weapon:
                var owned = OwnedWeapons[(int)pickup.Weapon];
                var gotAmmo = pickup.Ammo != AmmoType.None
                              && GiveAmmo(pickup.Ammo, dropped ? Math.Max(1, pickup.Amount / 2) : pickup.Amount);
                if (owned && !gotAmmo)
                    return false;
                if (!owned)
                {
                    OwnedWeapons[(int)pickup.Weapon] = true;
                    PendingWeapon = pickup.Weapon;
                }

                return true;
            case PickupKind.Key:
                Keys |= pickup.Key;
                return true;
            case PickupKind.Berserk:
                Berserk = true;
                BerserkFade = 0;
                Health = Math.Max(Health, MaxHealth);
                if (OwnedWeapons[(int)WeaponType.Fist])
                    PendingWeapon = WeaponType.Fist;
                return true;
            case PickupKind.Invulnerability:
                InvulnerabilityLeft = InvulnerabilityTics;
                return true;
            case PickupKind.RadiationSuit:
                RadiationSuitLeft = RadiationSuitTics;
                return true;
            case PickupKind.Invisibility:
                InvisibilityLeft = InvisibilityTics;
                return true;
            case PickupKind.ComputerMap:
                HasComputerMap = true;
                return true;
            case PickupKind.LightAmp:
                LightAmpLeft = LightAmpTics;
                return true;
            default:
                return false;
        }
    }

    /// <summary>What carries over to the next level.</summary>
    public PlayerLoadout ToLoadout() => new()
    {
        Health = Math.Max(Health, 1),
        Armor = Armor,
        ArmorClass = ArmorClass,
        Weapons = (bool[])OwnedWeapons.Clone(),
        Ammo = (int[])Ammo.Clone(),
        Backpack = Backpack,
        Weapon = Weapon
    };

    /// <summary>The screen tint palette: 0 none, 1-8 red (pain), 9-12 gold (pickup), 13 green (radiation suit).</summary>
    public int TintPalette()
    {
        var red = DamageCount;
        if (Berserk)
            red = Math.Max(red, 12 - Math.Min(12, BerserkFade >> 6));
        if (red > 0)
            return Math.Min(8, (red + 7) >> 3);
        if (BonusCount > 0)
            return 9 + Math.Min(3, (BonusCount + 7) >> 3);
        if (RadiationSuitLeft > 4 * 32 || (RadiationSuitLeft & 8) != 0)
            return RadiationSuitLeft > 0 ? 13 : 0;

        return 0;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private bool GiveAmmo(AmmoType type, int amount)
    {
        var max = MaxAmmoOf(type);
        if (Ammo[(int)type] >= max)
            return false;

        var hadNone = Ammo[(int)type] == 0;
        Ammo[(int)type] = Math.Min(max, Ammo[(int)type] + amount);
        if (!hadNone || Weapon is not (WeaponType.Fist or WeaponType.Pistol))
            return true;

        // Like Doom: picking up ammo for a better weapon while on fist or pistol switches to it.
        var better = type switch
        {
            AmmoType.Bullets => WeaponType.Chaingun,
            AmmoType.Shells => WeaponType.Shotgun,
            AmmoType.Cells => WeaponType.PlasmaGun,
            _ => Weapon
        };
        if (better != Weapon && OwnedWeapons[(int)better])
            PendingWeapon = better;
        return true;
    }
}
