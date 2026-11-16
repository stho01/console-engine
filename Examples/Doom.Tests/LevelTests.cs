using System;
using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Play;
using Doom.Tests.Fakes;
using NUnit.Framework;

namespace Doom.Tests;

/// <summary>Plays the test map tic by tic with scripted commands; no console needed.</summary>
public class LevelTests
{
    private const int Stimpack = 2011, Shotgun = 2001, Barrel = 2035, Zombie = 3004;

    private static readonly PlayerCommand Idle = new();

    private static PlayerCommand Forward() => new() { Forward = 1 };

    private static void Run(Level level, PlayerCommand command, int tics)
    {
        for (var i = 0; i < tics; i++)
            level.Tick(command);
    }

    private static int CountOf(Level level, int doomEdNum)
    {
        var type = ThingTypes.Find(doomEdNum).Id;
        var count = 0;
        foreach (var chunk in level.Things.GetChunks())
        {
            var things = chunk.GetSpan<Thing>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (things[i].Type == type)
                    count++;
            }
        }

        return count;
    }

    private static int CountWith<T>(Level level) where T : unmanaged => level.World.Query(Query.With<T>()).Count;

    [Test]
    public void Create_TestMap_PutsPlayerOnStart()
    {
        using var level = TestWad.CreateLevel();

        Assert.That(level.PlayerBody.X, Is.EqualTo(64));
        Assert.That(level.PlayerBody.Y, Is.EqualTo(128));
        Assert.That(level.Player.Health, Is.EqualTo(Player.StartHealth));
        Assert.That(level.Player.Weapon, Is.EqualTo(WeaponType.Pistol));
    }

    [Test]
    public void Create_TestMap_MakesTheStimpackACollectibleEntity()
    {
        using var level = TestWad.CreateLevel();

        Assert.That(CountOf(level, Stimpack), Is.EqualTo(1));
        Assert.That(CountWith<Collectible>(level), Is.EqualTo(1));
    }

    [Test]
    public void Tick_TouchingStimpack_HealsAndRemovesIt()
    {
        using var level = TestWad.CreateLevel(new PlayerLoadout { Health = 50 });

        Run(level, Idle, 1);

        Assert.That(level.Player.Health, Is.EqualTo(60));
        Assert.That(level.Message, Is.EqualTo(DoomTexts.GotStimpack));
        Assert.That(CountOf(level, Stimpack), Is.Zero);
    }

    [Test]
    public void Tick_TouchingStimpackAtFullHealth_LeavesIt()
    {
        using var level = TestWad.CreateLevel();

        Run(level, Idle, 1);

        Assert.That(level.Player.Health, Is.EqualTo(Player.StartHealth));
        Assert.That(CountOf(level, Stimpack), Is.EqualTo(1));
    }

    [Test]
    public void Tick_WalkingIntoWall_StopsAtTheWall()
    {
        using var level = TestWad.CreateLevel();
        level.PlayerBody.Angle = MathF.PI;

        Run(level, Forward(), 120);

        Assert.That(level.PlayerBody.X, Is.GreaterThanOrEqualTo(level.PlayerBody.Radius - 0.01f));
        Assert.That(level.PlayerBody.X, Is.LessThan(level.PlayerBody.Radius + 2));
    }

    [Test]
    public void Tick_WalkingIntoClosedDoor_IsBlocked()
    {
        using var level = TestWad.CreateLevel();

        Run(level, Forward(), 120);

        Assert.That(level.PlayerBody.X, Is.LessThanOrEqualTo(256 - level.PlayerBody.Radius + 0.01f));
    }

    [Test]
    public void Use_FacingDoor_OpensItToBelowTheLowestNeighbourCeiling()
    {
        using var level = TestWad.CreateLevel();
        MovePlayerTo(level, 220);

        level.Tick(new PlayerCommand { Use = true });
        Run(level, Idle, 80);

        Assert.That(level.Map.Sectors[TestWad.DoorSector].CeilingHeight, Is.EqualTo(124));
    }

    [Test]
    public void Tick_OpenDoorAfterWait_ClosesAgain()
    {
        using var level = TestWad.CreateLevel();
        MovePlayerTo(level, 220);

        level.Tick(new PlayerCommand { Use = true });
        Run(level, Idle, 80 + 150 + 80);

        Assert.That(level.Map.Sectors[TestWad.DoorSector].CeilingHeight, Is.EqualTo(0));
        Assert.That(level.Movers, Is.Empty);
    }

    [Test]
    public void ActivateLine_ExitSwitchFromFront_ExitsAndFlipsSwitch()
    {
        using var level = TestWad.CreateLevel();
        var line = level.Map.Lines[TestWad.ExitLine];
        var before = line.Front.Middle;

        var activated = level.ActivateLine(line, level.Player.Body, LineTrigger.Use, true);

        Assert.That(activated, Is.True);
        Assert.That(level.Exited, Is.True);
        Assert.That(level.SecretExit, Is.False);
        Assert.That(line.Front.Middle, Is.Not.EqualTo(before));
        Assert.That(line.Special, Is.EqualTo(0));
    }

    [Test]
    public void ActivateLine_ExitSwitchFromBehind_DoesNothing()
    {
        using var level = TestWad.CreateLevel();

        var activated = level.ActivateLine(level.Map.Lines[TestWad.ExitLine], level.Player.Body, LineTrigger.Use,
            false);

        Assert.That(activated, Is.False);
        Assert.That(level.Exited, Is.False);
    }

    [Test]
    public void Hitscan_AtMonster_DamagesAndKillsIt()
    {
        using var level = TestWad.CreateLevel();
        var zombie = level.Spawn(ThingTypes.Find(Zombie), 200, 128, float.NaN);
        level.Tick(Idle);

        var hit = level.Hitscan(level.Player.Body, 0, 2048, 100);

        ref var thing = ref level.World.Get<Thing>(zombie);
        Assert.That(hit, Is.EqualTo(zombie));
        Assert.That(thing.State, Is.EqualTo(ThingState.Dying));
        Assert.That(thing.Flags & ThingFlags.Solid, Is.EqualTo(ThingFlags.None));
        Assert.That(level.Stats.Kills, Is.EqualTo(1));
    }

    [Test]
    public void Tick_KilledZombie_DropsAClipAtTheEndOfTheTic()
    {
        using var level = TestWad.CreateLevel();
        level.Spawn(ThingTypes.Find(Zombie), 200, 128, float.NaN);
        var collectibles = CountWith<Collectible>(level);

        level.Tick(new PlayerCommand { Fire = true });
        level.Hitscan(level.Player.Body, 0, 2048, 100);
        level.Tick(Idle);

        Assert.That(CountWith<Collectible>(level), Is.EqualTo(collectibles + 1));
    }

    [Test]
    public void Tick_ZombieFacingThePlayer_WakesAndChases()
    {
        using var level = TestWad.CreateLevel();
        var zombie = level.Spawn(ThingTypes.Find(Zombie), 200, 128, float.NaN);
        level.World.Get<Body>(zombie).Angle = MathF.PI;

        Run(level, Idle, 4);

        Assert.That(level.World.Get<Thing>(zombie).State, Is.Not.EqualTo(ThingState.Idle));
        Assert.That(level.World.Get<Monster>(zombie).Target, Is.EqualTo(level.Player.Body));
    }

    [Test]
    public void Tick_RocketIntoWall_ExplodesHurtsThePlayerAndIsRemoved()
    {
        using var level = TestWad.CreateLevel();
        level.SpawnMissile(level.Player.Body, ThingTypes.Rocket, MathF.PI, Entity.Null);

        level.Tick(Idle);
        Assert.That(CountWith<Missile>(level), Is.EqualTo(1));
        Run(level, Idle, 30);

        Assert.That(CountWith<Missile>(level), Is.Zero);
        Assert.That(level.Player.Health, Is.LessThan(Player.StartHealth));
    }

    [Test]
    public void Hitscan_AtBarrel_ExplodesAndKillsTheZombieNextToIt()
    {
        using var level = TestWad.CreateLevel();
        level.Spawn(ThingTypes.Find(Barrel), 160, 128, float.NaN);
        var zombie = level.Spawn(ThingTypes.Find(Zombie), 160, 164, float.NaN);

        level.Hitscan(level.Player.Body, 0, 2048, 100);
        Run(level, Idle, 30);

        Assert.That(level.World.Get<Thing>(zombie).State, Is.EqualTo(ThingState.Dying).Or.EqualTo(ThingState.Dead));
        Assert.That(level.Stats.Kills, Is.EqualTo(1));
        Assert.That(CountOf(level, Barrel), Is.Zero);
    }

    [Test]
    public void Damage_PlayerWithGreenArmor_ArmorTakesAThird()
    {
        using var level = TestWad.CreateLevel(new PlayerLoadout { Armor = 100, ArmorClass = 1 });

        level.Damage(level.Player.Body, Entity.Null, Entity.Null, 30);

        Assert.That(level.Player.Armor, Is.EqualTo(90));
        Assert.That(level.Player.Health, Is.EqualTo(80));
    }

    [Test]
    public void Damage_PlayerToZero_DiesAndRestartsOnUse()
    {
        using var level = TestWad.CreateLevel();

        level.Damage(level.Player.Body, Entity.Null, Entity.Null, 500);
        Run(level, Idle, 60);
        level.Tick(new PlayerCommand { Use = true });

        Assert.That(level.Player.Dead, Is.True);
        Assert.That(level.RestartRequested, Is.True);
    }

    [Test]
    public void Tick_FireWithPistol_UsesABullet()
    {
        using var level = TestWad.CreateLevel();
        var bullets = level.Player.Ammo[(int)AmmoType.Bullets];

        level.Tick(new PlayerCommand { Fire = true });

        Assert.That(level.Player.Ammo[(int)AmmoType.Bullets], Is.EqualTo(bullets - 1));
    }

    [Test]
    public void Give_Shotgun_GivesWeaponShellsAndSwitches()
    {
        using var level = TestWad.CreateLevel();

        var taken = level.Player.Give(ThingTypes.Find(Shotgun).Pickup, false);

        Assert.That(taken, Is.True);
        Assert.That(level.Player.OwnedWeapons[(int)WeaponType.Shotgun], Is.True);
        Assert.That(level.Player.Ammo[(int)AmmoType.Shells], Is.EqualTo(8));
        Assert.That(level.Player.PendingWeapon, Is.EqualTo(WeaponType.Shotgun));
    }

    [Test]
    public void ToLoadout_AfterPickups_CarriesWeaponsAndAmmo()
    {
        using var level = TestWad.CreateLevel();
        level.Player.OwnedWeapons[(int)WeaponType.Chaingun] = true;
        level.Player.Ammo[(int)AmmoType.Bullets] = 123;
        level.Player.Keys = KeyCards.Red;

        var loadout = level.Player.ToLoadout();
        using var next = TestWad.CreateLevel(loadout);

        Assert.That(next.Player.OwnedWeapons[(int)WeaponType.Chaingun], Is.True);
        Assert.That(next.Player.Ammo[(int)AmmoType.Bullets], Is.EqualTo(123));
        Assert.That(next.Player.Keys, Is.EqualTo(KeyCards.None));
    }

    [TestCase(0f, 0f)]
    [TestCase(-0.5f, 2 * MathF.PI - 0.5f)]
    [TestCase(7f, 7f - 2 * MathF.PI)]
    public void WrapAngle_AnyAngle_IsInOneTurn(float angle, float expected) =>
        Assert.That(Level.WrapAngle(angle), Is.EqualTo(expected).Within(1e-4f));

    private static void MovePlayerTo(Level level, float x)
    {
        ref var body = ref level.PlayerBody;
        body.X = x;
        body.Sector = level.Map.SectorAt(body.X, body.Y).Index;
    }
}
