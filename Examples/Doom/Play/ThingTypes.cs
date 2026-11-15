using System.Collections.Generic;

namespace Doom.Play;

/// <summary>
/// The thing kinds this example knows, by Doom editor number (the type stored in a map's <c>THINGS</c> lump), plus
/// the projectiles and effects that are spawned during play. The pictures come from the WAD at run time; this table
/// only names the sprites and sets sizes and behaviour. Map things whose number is not listed are skipped.
/// </summary>
public static class ThingTypes
{
    //**********************************************************
    //** editor numbers with special meaning
    //**********************************************************

    public const int PlayerStart = 1;
    public const int TeleportDestination = 14;

    //**********************************************************
    //** spawned during play
    //**********************************************************

    public static readonly ThingInfo Player = new()
    {
        Sprite = "PLAY", Radius = 16, Height = 56, Health = 100,
        Flags = ThingFlags.Solid | ThingFlags.Shootable | ThingFlags.Invisible
    };

    public static readonly ThingInfo ImpFireball = Projectile("BAL1", 6, 8, 10, 3, 24);
    public static readonly ThingInfo BaronFireball = Projectile("BAL7", 6, 8, 15, 8, 64);
    public static readonly ThingInfo CacodemonBall = Projectile("BAL2", 6, 8, 10, 5, 40);

    public static readonly ThingInfo Rocket = new()
    {
        Sprite = "MISL", Frames = "A", Radius = 11, Height = 8, Speed = 20, DamageMin = 20, DamageMax = 160,
        SplashDamage = 128, DeathFrames = "BCD", RemoveAfterDeath = true,
        Flags = ThingFlags.Missile | ThingFlags.FullBright
    };

    public static readonly ThingInfo PlasmaBall = Projectile("PLSS", 13, 8, 25, 5, 40, "AB", "CDEFG");
    public static readonly ThingInfo BfgBall = Projectile("BFS1", 13, 8, 25, 100, 800, "AB", "CDEF");

    public static readonly ThingInfo BulletPuff = Effect("PUFF", "ABCD", 4);
    public static readonly ThingInfo Blood = Effect("BLUD", "CBA", 6);
    public static readonly ThingInfo TeleportFog = Effect("TFOG", "ABABCDEFGHIJ", 6);
    public static readonly ThingInfo ItemFog = Effect("IFOG", "ABABCDE", 6);

    //**********************************************************
    //** placed in maps
    //**********************************************************

    private static readonly Dictionary<int, ThingInfo> ByDoomEdNum = Build();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The kind of thing with editor number <paramref name="doomEdNum"/>, or null when unknown.</summary>
    public static ThingInfo Find(int doomEdNum) => ByDoomEdNum.GetValueOrDefault(doomEdNum);

    //**********************************************************
    //** internals
    //**********************************************************

    private static ThingInfo Projectile(string sprite, float radius, float height, float speed, int min, int max,
        string frames = "AB", string death = "CDE") => new()
    {
        Sprite = sprite, Frames = frames, FrameTics = 4, Radius = radius, Height = height, Speed = speed,
        DamageMin = min, DamageMax = max, DeathFrames = death, RemoveAfterDeath = true,
        Flags = ThingFlags.Missile | ThingFlags.FullBright
    };

    private static ThingInfo Effect(string sprite, string frames, int tics) => new()
    {
        Sprite = sprite, DeathFrames = frames, FrameTics = tics, Frames = frames[..1], Radius = 1, Height = 1,
        RemoveAfterDeath = true, IsEffect = true, Flags = ThingFlags.FullBright
    };

    private static Dictionary<int, ThingInfo> Build()
    {
        var table = new Dictionary<int, ThingInfo>();

        void Add(int number, ThingInfo info) => table[number] = new ThingInfo
        {
            DoomEdNum = number, Sprite = info.Sprite, Frames = info.Frames, FrameTics = info.FrameTics,
            Radius = info.Radius, Height = info.Height, Flags = info.Flags, Health = info.Health,
            DeathFrames = info.DeathFrames, RemoveAfterDeath = info.RemoveAfterDeath, DeathSprite = info.DeathSprite,
            IsEffect = info.IsEffect, Speed = info.Speed,
            DamageMin = info.DamageMin, DamageMax = info.DamageMax, SplashDamage = info.SplashDamage,
            Monster = info.Monster, Pickup = info.Pickup
        };

        void Decoration(int number, string sprite, string frames = "A", bool solid = false, bool bright = false,
            float radius = 16, float height = 16, bool hanging = false)
        {
            var flags = (solid ? ThingFlags.Solid : ThingFlags.None) | (bright ? ThingFlags.FullBright : ThingFlags.None)
                        | (hanging ? ThingFlags.Hanging : ThingFlags.None);
            Add(number, new ThingInfo { Sprite = sprite, Frames = frames, Radius = radius, Height = height, Flags = flags });
        }

        void Item(int number, string sprite, Pickup pickup, string frames = "A", bool counts = false,
            bool bright = false)
        {
            var flags = ThingFlags.Pickup | (counts ? ThingFlags.CountItem : ThingFlags.None)
                        | (bright ? ThingFlags.FullBright : ThingFlags.None);
            Add(number, new ThingInfo
            {
                Sprite = sprite, Frames = frames, FrameTics = 6, Radius = 20, Height = 16, Flags = flags,
                Pickup = pickup
            });
        }

        // Monsters
        const ThingFlags monster = ThingFlags.Solid | ThingFlags.Shootable | ThingFlags.Monster | ThingFlags.CountKill;
        Add(3004, new ThingInfo
        {
            Sprite = "POSS", Frames = "AB", Radius = 20, Height = 56, Health = 20, Flags = monster,
            DeathFrames = "HIJKL",
            Monster = new MonsterInfo
            {
                Speed = 2, PainChance = 200, Attack = MonsterAttack.Hitscan, DamageMin = 3, DamageMax = 15,
                AttackFrames = "EF", PainFrames = "G", Drops = 2007
            }
        });
        Add(9, new ThingInfo
        {
            Sprite = "SPOS", Frames = "AB", Radius = 20, Height = 56, Health = 30, Flags = monster,
            DeathFrames = "HIJKL",
            Monster = new MonsterInfo
            {
                Speed = 2.7f, PainChance = 170, Attack = MonsterAttack.Hitscan, Pellets = 3, DamageMin = 3,
                DamageMax = 15, AttackFrames = "EF", PainFrames = "G", Drops = 2001
            }
        });
        Add(3001, new ThingInfo
        {
            Sprite = "TROO", Frames = "AB", Radius = 20, Height = 56, Health = 60, Flags = monster,
            DeathFrames = "IJKLM",
            Monster = new MonsterInfo
            {
                Speed = 2.7f, PainChance = 200, Attack = MonsterAttack.MeleeOrMissile, MeleeDamageMin = 3,
                MeleeDamageMax = 24, Missile = ImpFireball, AttackFrames = "EFG", PainFrames = "H"
            }
        });
        var demon = new MonsterInfo
        {
            Speed = 5, PainChance = 180, Attack = MonsterAttack.Melee, MeleeDamageMin = 4, MeleeDamageMax = 40,
            AttackFrames = "EFG", PainFrames = "H"
        };
        Add(3002, new ThingInfo
        {
            Sprite = "SARG", Frames = "AB", Radius = 30, Height = 56, Health = 150, Flags = monster,
            DeathFrames = "IJKLMN", Monster = demon
        });
        Add(58, new ThingInfo
        {
            Sprite = "SARG", Frames = "AB", Radius = 30, Height = 56, Health = 150,
            Flags = monster | ThingFlags.Shadow, DeathFrames = "IJKLMN", Monster = demon
        });
        Add(3006, new ThingInfo
        {
            Sprite = "SKUL", Frames = "AB", Radius = 16, Height = 56, Health = 100,
            Flags = ThingFlags.Solid | ThingFlags.Shootable | ThingFlags.Monster | ThingFlags.Float
                    | ThingFlags.FullBright,
            DeathFrames = "FGHIJK", RemoveAfterDeath = true,
            Monster = new MonsterInfo
            {
                Speed = 2, PainChance = 256, Attack = MonsterAttack.Charge, DamageMin = 3, DamageMax = 24,
                WalkFrames = "AB", AttackFrames = "CD", PainFrames = "E"
            }
        });
        Add(3005, new ThingInfo
        {
            Sprite = "HEAD", Frames = "A", Radius = 31, Height = 56, Health = 400,
            Flags = monster | ThingFlags.Float, DeathFrames = "GHIJKL",
            Monster = new MonsterInfo
            {
                Speed = 2.7f, PainChance = 128, Attack = MonsterAttack.MeleeOrMissile, MeleeDamageMin = 10,
                MeleeDamageMax = 60, Missile = CacodemonBall, WalkFrames = "A", AttackFrames = "BCD", PainFrames = "E"
            }
        });
        Add(3003, new ThingInfo
        {
            Sprite = "BOSS", Frames = "AB", Radius = 24, Height = 64, Health = 1000, Flags = monster,
            DeathFrames = "IJKLMNO",
            Monster = new MonsterInfo
            {
                Speed = 2.7f, PainChance = 50, Attack = MonsterAttack.MeleeOrMissile, MeleeDamageMin = 10,
                MeleeDamageMax = 80, Missile = BaronFireball, AttackFrames = "EFG", PainFrames = "H"
            }
        });

        // Barrel
        Add(2035, new ThingInfo
        {
            Sprite = "BAR1", Frames = "AB", FrameTics = 6, Radius = 10, Height = 42, Health = 20,
            Flags = ThingFlags.Solid | ThingFlags.Shootable | ThingFlags.NoBlood, DeathSprite = "BEXP",
            DeathFrames = "ABCDE", RemoveAfterDeath = true, SplashDamage = 128
        });

        // Health and armor
        Item(2011, "STIM", new Pickup(PickupKind.Health, DoomTexts.GotStimpack, 10));
        Item(2012, "MEDI", new Pickup(PickupKind.Health, DoomTexts.GotMedikit, 25));
        Item(2014, "BON1", new Pickup(PickupKind.HealthBonus, DoomTexts.GotHealthBonus, 1), "ABCDCB", true);
        Item(2013, "SOUL", new Pickup(PickupKind.SoulSphere, DoomTexts.GotSoulSphere, 100), "ABCDCB", true, true);
        Item(2015, "BON2", new Pickup(PickupKind.ArmorBonus, DoomTexts.GotArmorBonus, 1), "ABCDCB", true);
        Item(2018, "ARM1", new Pickup(PickupKind.Armor, DoomTexts.GotGreenArmor, 100, ArmorClass: 1), "AB");
        Item(2019, "ARM2", new Pickup(PickupKind.Armor, DoomTexts.GotBlueArmor, 200, ArmorClass: 2), "AB");

        // Ammo
        Item(2007, "CLIP", new Pickup(PickupKind.Ammo, DoomTexts.GotBullets, 10, AmmoType.Bullets));
        Item(2048, "AMMO", new Pickup(PickupKind.Ammo, DoomTexts.GotBullets, 50, AmmoType.Bullets));
        Item(2008, "SHEL", new Pickup(PickupKind.Ammo, DoomTexts.GotShells, 4, AmmoType.Shells));
        Item(2049, "SBOX", new Pickup(PickupKind.Ammo, DoomTexts.GotShells, 20, AmmoType.Shells));
        Item(2010, "ROCK", new Pickup(PickupKind.Ammo, DoomTexts.GotRockets, 1, AmmoType.Rockets));
        Item(2046, "BROK", new Pickup(PickupKind.Ammo, DoomTexts.GotRockets, 5, AmmoType.Rockets));
        Item(2047, "CELL", new Pickup(PickupKind.Ammo, DoomTexts.GotCells, 20, AmmoType.Cells));
        Item(17, "CELP", new Pickup(PickupKind.Ammo, DoomTexts.GotCells, 100, AmmoType.Cells));
        Item(8, "BPAK", new Pickup(PickupKind.Backpack, DoomTexts.GotBackpack));

        // Weapons
        Item(2005, "CSAW", new Pickup(PickupKind.Weapon, DoomTexts.GotChainsaw, 0, Weapon: WeaponType.Chainsaw));
        Item(2001, "SHOT", new Pickup(PickupKind.Weapon, DoomTexts.GotShotgun, 8, AmmoType.Shells,
            WeaponType.Shotgun));
        Item(2002, "MGUN", new Pickup(PickupKind.Weapon, DoomTexts.GotChaingun, 20, AmmoType.Bullets,
            WeaponType.Chaingun));
        Item(2003, "LAUN", new Pickup(PickupKind.Weapon, DoomTexts.GotRocketLauncher, 2, AmmoType.Rockets,
            WeaponType.RocketLauncher));
        Item(2004, "PLAS", new Pickup(PickupKind.Weapon, DoomTexts.GotPlasmaGun, 40, AmmoType.Cells,
            WeaponType.PlasmaGun));
        Item(2006, "BFUG", new Pickup(PickupKind.Weapon, DoomTexts.GotBfg, 40, AmmoType.Cells, WeaponType.Bfg));

        // Keys
        Item(5, "BKEY", new Pickup(PickupKind.Key, DoomTexts.GotBlueKey, Key: KeyCards.Blue), "AB", bright: true);
        Item(6, "YKEY", new Pickup(PickupKind.Key, DoomTexts.GotYellowKey, Key: KeyCards.Yellow), "AB", bright: true);
        Item(13, "RKEY", new Pickup(PickupKind.Key, DoomTexts.GotRedKey, Key: KeyCards.Red), "AB", bright: true);
        Item(40, "BSKU", new Pickup(PickupKind.Key, DoomTexts.GotBlueKey, Key: KeyCards.Blue), "AB", bright: true);
        Item(39, "YSKU", new Pickup(PickupKind.Key, DoomTexts.GotYellowKey, Key: KeyCards.Yellow), "AB", bright: true);
        Item(38, "RSKU", new Pickup(PickupKind.Key, DoomTexts.GotRedKey, Key: KeyCards.Red), "AB", bright: true);

        // Power-ups
        Item(2023, "PSTR", new Pickup(PickupKind.Berserk, DoomTexts.GotBerserk), counts: true, bright: true);
        Item(2022, "PINV", new Pickup(PickupKind.Invulnerability, DoomTexts.GotInvulnerability), "ABCD", true, true);
        Item(2025, "SUIT", new Pickup(PickupKind.RadiationSuit, DoomTexts.GotRadiationSuit), bright: true);
        Item(2024, "PINS", new Pickup(PickupKind.Invisibility, DoomTexts.GotInvisibility), "ABCD", true, true);
        Item(2026, "PMAP", new Pickup(PickupKind.ComputerMap, DoomTexts.GotComputerMap), "ABCDCB", true, true);
        Item(2045, "PVIS", new Pickup(PickupKind.LightAmp, DoomTexts.GotLightAmp), "AB", true, true);

        // Lights and obstacles
        Decoration(2028, "COLU", solid: true, bright: true);
        Decoration(34, "CAND", bright: true);
        Decoration(35, "CBRA", solid: true, bright: true);
        Decoration(44, "TBLU", "ABCD", true, true);
        Decoration(45, "TGRN", "ABCD", true, true);
        Decoration(46, "TRED", "ABCD", true, true);
        Decoration(55, "SMBT", "ABCD", true, true);
        Decoration(56, "SMGT", "ABCD", true, true);
        Decoration(57, "SMRT", "ABCD", true, true);
        Decoration(48, "ELEC", solid: true);
        Decoration(30, "COL1", solid: true);
        Decoration(31, "COL2", solid: true);
        Decoration(32, "COL3", solid: true);
        Decoration(33, "COL4", solid: true);
        Decoration(36, "COL5", "AB", true);
        Decoration(37, "COL6", solid: true);
        Decoration(41, "CEYE", "ABCB", true, true);
        Decoration(42, "FSKU", "ABC", true, true);
        Decoration(43, "TRE1", solid: true);
        Decoration(47, "SMIT", solid: true);
        Decoration(54, "TRE2", solid: true, radius: 32);
        Decoration(25, "POL1", solid: true);
        Decoration(26, "POL6", "AB", true);
        Decoration(27, "POL4", solid: true);
        Decoration(28, "POL2", solid: true);
        Decoration(29, "POL3", "AB", true, true);
        Decoration(24, "POL5");

        // Hanging bodies: 49-53 block, 59-63 do not
        Decoration(49, "GOR1", "ABCB", true, height: 68, hanging: true);
        Decoration(50, "GOR2", solid: true, height: 84, hanging: true);
        Decoration(51, "GOR3", solid: true, height: 84, hanging: true);
        Decoration(52, "GOR4", solid: true, height: 68, hanging: true);
        Decoration(53, "GOR5", solid: true, height: 52, hanging: true);
        Decoration(63, "GOR1", "ABCB", height: 68, hanging: true);
        Decoration(59, "GOR2", height: 84, hanging: true);
        Decoration(61, "GOR3", height: 52, hanging: true);
        Decoration(60, "GOR4", height: 68, hanging: true);
        Decoration(62, "GOR5", height: 52, hanging: true);

        // Corpses
        Decoration(15, "PLAY", "N");
        Decoration(10, "PLAY", "W");
        Decoration(12, "PLAY", "W");
        Decoration(18, "POSS", "L");
        Decoration(19, "SPOS", "L");
        Decoration(20, "TROO", "M");
        Decoration(21, "SARG", "N");
        Decoration(22, "HEAD", "L");

        // Never drawn
        Add(TeleportDestination, new ThingInfo { Flags = ThingFlags.Invisible });

        return table;
    }
}
