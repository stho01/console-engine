using System;
using Doom.Play;
using Doom.Tests.Fakes;
using NUnit.Framework;

namespace Doom.Tests;

/// <summary>Plays the test map tic by tic with scripted commands; no console needed.</summary>
public class LevelTests
{
    private static readonly PlayerCommand Idle = new();

    private static PlayerCommand Forward() => new() { Forward = 1 };

    private static void Run(Level level, PlayerCommand command, int tics)
    {
        for (var i = 0; i < tics; i++)
            level.Tick(command);
    }

    [Test]
    public void Create_TestMap_PutsPlayerOnStart()
    {
        var level = TestWad.CreateLevel();

        var body = level.Player.Body;
        Assert.That(body.X, Is.EqualTo(64));
        Assert.That(body.Y, Is.EqualTo(128));
        Assert.That(level.Player.Health, Is.EqualTo(Player.StartHealth));
        Assert.That(level.Player.Weapon, Is.EqualTo(WeaponType.Pistol));
    }

    [Test]
    public void Tick_TouchingStimpack_HealsAndRemovesIt()
    {
        var level = TestWad.CreateLevel(new PlayerLoadout { Health = 50 });

        Run(level, Idle, 1);

        Assert.That(level.Player.Health, Is.EqualTo(60));
        Assert.That(level.Message, Is.EqualTo(DoomTexts.GotStimpack));
    }

    [Test]
    public void Tick_TouchingStimpackAtFullHealth_LeavesIt()
    {
        var level = TestWad.CreateLevel();

        Run(level, Idle, 1);

        Assert.That(level.Player.Health, Is.EqualTo(Player.StartHealth));
        Assert.That(level.Actors, Has.Some.Matches<Actor>(actor => actor.Info.DoomEdNum == 2011));
    }

    [Test]
    public void Tick_WalkingIntoWall_StopsAtTheWall()
    {
        var level = TestWad.CreateLevel();
        level.Player.Body.Angle = MathF.PI;

        Run(level, Forward(), 120);

        Assert.That(level.Player.Body.X, Is.GreaterThanOrEqualTo(level.Player.Body.Radius - 0.01f));
        Assert.That(level.Player.Body.X, Is.LessThan(level.Player.Body.Radius + 2));
    }

    [Test]
    public void Tick_WalkingIntoClosedDoor_IsBlocked()
    {
        var level = TestWad.CreateLevel();

        Run(level, Forward(), 120);

        Assert.That(level.Player.Body.X, Is.LessThanOrEqualTo(256 - level.Player.Body.Radius + 0.01f));
    }

    [Test]
    public void Use_FacingDoor_OpensItToBelowTheLowestNeighbourCeiling()
    {
        var level = TestWad.CreateLevel();
        var body = level.Player.Body;
        body.X = 220;
        body.Sector = level.Map.SectorAt(body.X, body.Y);

        level.Tick(new PlayerCommand { Use = true });
        Run(level, Idle, 80);

        Assert.That(level.Map.Sectors[TestWad.DoorSector].CeilingHeight, Is.EqualTo(124));
    }

    [Test]
    public void Tick_OpenDoorAfterWait_ClosesAgain()
    {
        var level = TestWad.CreateLevel();
        var body = level.Player.Body;
        body.X = 220;
        body.Sector = level.Map.SectorAt(body.X, body.Y);

        level.Tick(new PlayerCommand { Use = true });
        Run(level, Idle, 80 + 150 + 80);

        Assert.That(level.Map.Sectors[TestWad.DoorSector].CeilingHeight, Is.EqualTo(0));
        Assert.That(level.Movers, Is.Empty);
    }

    [Test]
    public void ActivateLine_ExitSwitchFromFront_ExitsAndFlipsSwitch()
    {
        var level = TestWad.CreateLevel();
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
        var level = TestWad.CreateLevel();

        var activated = level.ActivateLine(level.Map.Lines[TestWad.ExitLine], level.Player.Body, LineTrigger.Use,
            false);

        Assert.That(activated, Is.False);
        Assert.That(level.Exited, Is.False);
    }

    [Test]
    public void Hitscan_AtMonster_DamagesAndKillsIt()
    {
        var level = TestWad.CreateLevel();
        var zombie = level.Spawn(ThingTypes.Find(3004), 200, 128, float.NaN);
        level.Tick(Idle);

        var hit = level.Hitscan(level.Player.Body, 0, 2048, 100);

        Assert.That(hit, Is.SameAs(zombie));
        Assert.That(zombie.State, Is.EqualTo(ActorState.Dying));
        Assert.That(zombie.Has(ThingFlags.Solid), Is.False);
        Assert.That(level.Stats.Kills, Is.EqualTo(1));
    }

    [Test]
    public void Damage_PlayerWithGreenArmor_ArmorTakesAThird()
    {
        var level = TestWad.CreateLevel(new PlayerLoadout { Armor = 100, ArmorClass = 1 });

        level.Damage(level.Player.Body, null, null, 30);

        Assert.That(level.Player.Armor, Is.EqualTo(90));
        Assert.That(level.Player.Health, Is.EqualTo(80));
    }

    [Test]
    public void Damage_PlayerToZero_DiesAndRestartsOnUse()
    {
        var level = TestWad.CreateLevel();

        level.Damage(level.Player.Body, null, null, 500);
        Run(level, Idle, 60);
        level.Tick(new PlayerCommand { Use = true });

        Assert.That(level.Player.Dead, Is.True);
        Assert.That(level.RestartRequested, Is.True);
    }

    [Test]
    public void Tick_FireWithPistol_UsesABullet()
    {
        var level = TestWad.CreateLevel();
        var bullets = level.Player.Ammo[(int)AmmoType.Bullets];

        level.Tick(new PlayerCommand { Fire = true });

        Assert.That(level.Player.Ammo[(int)AmmoType.Bullets], Is.EqualTo(bullets - 1));
    }

    [Test]
    public void TryPickup_Shotgun_GivesWeaponShellsAndSwitches()
    {
        var level = TestWad.CreateLevel();
        var shotgun = level.Spawn(ThingTypes.Find(2001), 10, 10, float.NaN);

        var taken = level.TryPickup(level.Player, shotgun);

        Assert.That(taken, Is.True);
        Assert.That(level.Player.OwnedWeapons[(int)WeaponType.Shotgun], Is.True);
        Assert.That(level.Player.Ammo[(int)AmmoType.Shells], Is.EqualTo(8));
        Assert.That(level.Player.PendingWeapon, Is.EqualTo(WeaponType.Shotgun));
    }

    [Test]
    public void ToLoadout_AfterPickups_CarriesWeaponsAndAmmo()
    {
        var level = TestWad.CreateLevel();
        level.Player.OwnedWeapons[(int)WeaponType.Chaingun] = true;
        level.Player.Ammo[(int)AmmoType.Bullets] = 123;
        level.Player.Keys = KeyCards.Red;

        var loadout = level.Player.ToLoadout();
        var next = TestWad.CreateLevel(loadout);

        Assert.That(next.Player.OwnedWeapons[(int)WeaponType.Chaingun], Is.True);
        Assert.That(next.Player.Ammo[(int)AmmoType.Bullets], Is.EqualTo(123));
        Assert.That(next.Player.Keys, Is.EqualTo(KeyCards.None));
    }

    [TestCase(0f, 0f)]
    [TestCase(-0.5f, 2 * MathF.PI - 0.5f)]
    [TestCase(7f, 7f - 2 * MathF.PI)]
    public void WrapAngle_AnyAngle_IsInOneTurn(float angle, float expected) =>
        Assert.That(Level.WrapAngle(angle), Is.EqualTo(expected).Within(1e-4f));
}
