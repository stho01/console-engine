using System;
using Doom.Maps;

namespace Doom.Play;

public sealed partial class Level
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Speed gained per tic of holding forward (walking), world units per tic.</summary>
    private const float WalkThrust = 0.78f;

    private const float StrafeThrust = 0.75f;
    private const float RunFactor = 2f;

    /// <summary>Momentum kept per tic on the ground.</summary>
    private const float Friction = 0.90625f;

    /// <summary>Turning speed, radians per tic.</summary>
    private const float TurnSpeed = 0.085f;

    private const float UseRange = 64;
    private const float MaxBob = 16;
    private const float BobSpeed = MathF.PI * 2 / 20;

    /// <summary>How far down the eyes sink when the player dies.</summary>
    private const float DeadViewHeight = 6;

    /// <summary>Tics between hits of a damaging floor.</summary>
    private const int FloorDamageInterval = 32;

    private const int WeaponMoveSpeed = 12;

    //**********************************************************
    //** internals
    //**********************************************************

    private void TickPlayer(in PlayerCommand command)
    {
        var player = Player;
        var body = player.Body;
        if (player.Dead)
        {
            TickDeadPlayer(player, command);
            return;
        }

        if (command.ToggleRun)
            player.Running = !player.Running;

        body.Angle = WrapAngle(body.Angle + command.Turn * TurnSpeed);
        var onGround = body.Z <= body.FloorZ;
        if (onGround)
        {
            var factor = player.Running ? RunFactor : 1f;
            var cos = MathF.Cos(body.Angle);
            var sin = MathF.Sin(body.Angle);
            body.MomX += (cos * command.Forward * WalkThrust + sin * command.Strafe * StrafeThrust) * factor;
            body.MomY += (sin * command.Forward * WalkThrust - cos * command.Strafe * StrafeThrust) * factor;
        }

        MoveByMomentum(body);
        if (Exited)
            return;

        if (body.Sector.Moved)
        {
            CheckPosition(body, body.X, body.Y, out var check);
            body.FloorZ = check.FloorZ;
            body.CeilingZ = check.CeilingZ;
        }

        var previousZ = body.Z;
        if (body.Z < body.FloorZ)
        {
            player.CurrentViewHeight -= body.FloorZ - body.Z;
            body.Z = body.FloorZ;
        }

        ApplyGravity(body);
        if (previousZ > body.Z + 8 && body.Z <= body.FloorZ && body.MomZ == 0)
            player.CurrentViewHeight -= MathF.Min(8, (previousZ - body.Z) / 4);

        if (body.Z <= body.FloorZ)
        {
            body.MomX *= Friction;
            body.MomY *= Friction;
            if (MathF.Abs(body.MomX) < 0.05f && MathF.Abs(body.MomY) < 0.05f && command.Forward == 0
                && command.Strafe == 0)
            {
                body.MomX = 0;
                body.MomY = 0;
            }
        }

        UpdateView(player);
        TouchPickups(player);
        if (command.Use)
            UseLines(body);
        TickWeapon(player, command);
        TickSectorSpecial(player);
        TickPowerUps(player);
    }

    private void TickDeadPlayer(Player player, in PlayerCommand command)
    {
        var body = player.Body;
        body.MomX = 0;
        body.MomY = 0;
        if (player.CurrentViewHeight > DeadViewHeight)
            player.CurrentViewHeight = MathF.Max(DeadViewHeight, player.CurrentViewHeight - 1);
        player.ViewZ = body.Z + player.CurrentViewHeight;
        if (player.DamageCount > 0)
            player.DamageCount--;
        if (player.CurrentViewHeight <= DeadViewHeight && (command.Use || command.Fire))
            RestartRequested = true;
    }

    /// <summary>Eye height: recovers from steps and landings, plus a bob that grows with speed.</summary>
    private static void UpdateView(Player player)
    {
        var body = player.Body;
        var speedSquared = body.MomX * body.MomX + body.MomY * body.MomY;
        player.Bob = MathF.Min(MaxBob, speedSquared / 4) / 2;
        player.BobPhase = (player.BobPhase + BobSpeed) % (MathF.PI * 2);

        if (player.CurrentViewHeight < Player.ViewHeight)
            player.CurrentViewHeight = MathF.Min(Player.ViewHeight,
                player.CurrentViewHeight + MathF.Max(1, (Player.ViewHeight - player.CurrentViewHeight) / 4));

        var bob = player.Bob * MathF.Sin(player.BobPhase);
        var viewZ = body.Z + player.CurrentViewHeight + bob;
        player.ViewZ = MathF.Min(viewZ, body.CeilingZ - 4);
    }

    /// <summary>
    /// Presses the first line within <see cref="UseRange"/> straight ahead: one with a use special activates (from
    /// its front side); a solid wall or closed opening stops the search.
    /// </summary>
    private void UseLines(Actor body)
    {
        var cos = MathF.Cos(body.Angle);
        var sin = MathF.Sin(body.Angle);
        var ex = body.X + cos * UseRange;
        var ey = body.Y + sin * UseRange;
        Line nearest = null;
        var nearestFraction = float.MaxValue;
        var lines = Map.Lines;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!SegmentsCross(body.X, body.Y, ex, ey, line, out var fraction) || fraction >= nearestFraction)
                continue;

            var stops = line.Special != 0 || line.Back == null
                        || MathF.Min(line.FrontSector.CeilingHeight, line.BackSector.CeilingHeight)
                        <= MathF.Max(line.FrontSector.FloorHeight, line.BackSector.FloorHeight);
            if (!stops)
                continue;

            nearest = line;
            nearestFraction = fraction;
        }

        if (nearest is { Special: not 0 })
            ActivateLine(nearest, body, LineTrigger.Use, nearest.SideOf(body.X, body.Y) > 0);
    }

    //**********************************************************
    //** weapons
    //**********************************************************

    private void TickWeapon(Player player, in PlayerCommand command)
    {
        if (command.WeaponSlot > 0)
            SelectSlot(player, command.WeaponSlot);

        if (player.PendingWeapon != player.Weapon)
        {
            player.WeaponOffset = Math.Min(Player.WeaponLowered, player.WeaponOffset + WeaponMoveSpeed);
            if (player.WeaponOffset >= Player.WeaponLowered)
                player.Weapon = player.PendingWeapon;
        }
        else if (player.WeaponOffset > 0)
        {
            player.WeaponOffset = Math.Max(0, player.WeaponOffset - WeaponMoveSpeed);
        }

        if (player.RefireTics > 0)
            player.RefireTics--;
        if (player.FireTics > 0)
            player.FireTics--;
        if (player.FlashTics > 0)
            player.FlashTics--;

        if (!command.Fire)
        {
            if (player.RefireTics == 0)
                player.Refiring = false;
            return;
        }

        if (player.RefireTics > 0 || player.WeaponOffset > 0 || player.PendingWeapon != player.Weapon)
            return;

        if (!player.HasAmmoFor(player.Weapon))
        {
            player.PendingWeapon = BestWeapon(player);
            return;
        }

        FireWeapon(player);
    }

    /// <summary>A number key picks the weapon in that slot; 1 toggles fist and chainsaw.</summary>
    private static void SelectSlot(Player player, int slot)
    {
        if (slot == 1)
        {
            var sawOwned = player.OwnedWeapons[(int)WeaponType.Chainsaw];
            player.PendingWeapon = sawOwned && player.PendingWeapon != WeaponType.Chainsaw
                ? WeaponType.Chainsaw
                : WeaponType.Fist;
            return;
        }

        for (var i = 0; i < Weapons.All.Length; i++)
        {
            if (Weapons.All[i].Slot != slot || !player.OwnedWeapons[i])
                continue;

            player.PendingWeapon = Weapons.All[i].Type;
            return;
        }
    }

    private static WeaponType BestWeapon(Player player)
    {
        foreach (var type in Weapons.Preference)
        {
            if (player.OwnedWeapons[(int)type] && player.HasAmmoFor(type) && type != WeaponType.Bfg)
                return type;
        }

        return WeaponType.Fist;
    }

    private void FireWeapon(Player player)
    {
        var info = Weapons.Get(player.Weapon);
        var body = player.Body;
        if (info.Ammo != AmmoType.None)
            player.Ammo[(int)info.Ammo] -= info.AmmoPerShot;

        player.RefireTics = info.RefireTics;
        player.FireTics = info.RefireTics;
        player.FlashTics = info.FlashSprite == null ? 0 : Math.Max(4, info.RefireTics / 2);
        switch (info.Kind)
        {
            case WeaponKind.Melee:
                var damage = RandomRange(info.DamageMin, info.DamageMax);
                if (player.Berserk && info.Type == WeaponType.Fist)
                    damage *= 10;
                var target = Hitscan(body, body.Angle + (Random.Next(256) - Random.Next(256)) / 255f * 0.05f,
                    info.Range, damage);
                if (target != null)
                    body.Angle = AngleTo(body, target.X, target.Y);
                break;
            case WeaponKind.Hitscan:
                for (var i = 0; i < info.Pellets; i++)
                {
                    var accurate = info.FirstShotAccurate && !player.Refiring;
                    var spread = accurate ? 0 : (Random.Next(256) - Random.Next(256)) / 255f * info.Spread;
                    Hitscan(body, body.Angle + spread, info.Range, RandomRange(info.DamageMin, info.DamageMax));
                }

                break;
            case WeaponKind.Missile:
                SpawnMissile(body, info.Missile, body.Angle, null);
                break;
        }

        player.Refiring = true;
        if (info.Type != WeaponType.Fist)
            NoiseAlert(body.Sector);
    }

    //**********************************************************
    //** pickups
    //**********************************************************

    private void TouchPickups(Player player)
    {
        var body = player.Body;
        for (var i = 0; i < _actors.Count; i++)
        {
            var item = _actors[i];
            if (item.Removed || !item.Has(ThingFlags.Pickup))
                continue;

            var reach = body.Radius + item.Radius;
            if (MathF.Abs(item.X - body.X) >= reach || MathF.Abs(item.Y - body.Y) >= reach)
                continue;
            if (item.Z > body.Z + body.Height || item.Z + 8 < body.Z)
                continue;
            if (!TryPickup(player, item))
                continue;

            item.Removed = true;
            player.BonusCount = Math.Min(player.BonusCount + 6, 24);
            if (item.Info.Has(ThingFlags.CountItem))
                Stats.Items++;
        }
    }

    /// <summary>Applies a pickup. Returns false (and leaves it) when the player can't use it now.</summary>
    public bool TryPickup(Player player, Actor item)
    {
        var pickup = item.Info.Pickup;
        switch (pickup.Kind)
        {
            case PickupKind.Health:
                if (player.Health >= Player.MaxHealth)
                    return false;
                player.Health = Math.Min(Player.MaxHealth, player.Health + pickup.Amount);
                break;
            case PickupKind.HealthBonus:
                player.Health = Math.Min(Player.MaxBonusHealth, player.Health + pickup.Amount);
                break;
            case PickupKind.SoulSphere:
                player.Health = Math.Min(Player.MaxBonusHealth, player.Health + pickup.Amount);
                break;
            case PickupKind.ArmorBonus:
                player.Armor = Math.Min(Player.MaxArmor, player.Armor + pickup.Amount);
                if (player.ArmorClass == 0)
                    player.ArmorClass = 1;
                break;
            case PickupKind.Armor:
                if (player.Armor >= pickup.Amount)
                    return false;
                player.Armor = pickup.Amount;
                player.ArmorClass = pickup.ArmorClass;
                break;
            case PickupKind.Ammo:
                if (!GiveAmmo(player, pickup.Ammo, item.Dropped ? Math.Max(1, pickup.Amount / 2) : pickup.Amount))
                    return false;
                break;
            case PickupKind.Backpack:
                if (!player.Backpack)
                    player.Backpack = true;
                GiveAmmo(player, AmmoType.Bullets, 10);
                GiveAmmo(player, AmmoType.Shells, 4);
                GiveAmmo(player, AmmoType.Rockets, 1);
                GiveAmmo(player, AmmoType.Cells, 20);
                break;
            case PickupKind.Weapon:
                var owned = player.OwnedWeapons[(int)pickup.Weapon];
                var gotAmmo = pickup.Ammo != AmmoType.None && GiveAmmo(player, pickup.Ammo,
                    item.Dropped ? Math.Max(1, pickup.Amount / 2) : pickup.Amount);
                if (owned && !gotAmmo)
                    return false;
                if (!owned)
                {
                    player.OwnedWeapons[(int)pickup.Weapon] = true;
                    player.PendingWeapon = pickup.Weapon;
                }

                break;
            case PickupKind.Key:
                player.Keys |= pickup.Key;
                break;
            case PickupKind.Berserk:
                player.Berserk = true;
                player.BerserkFade = 0;
                player.Health = Math.Max(player.Health, Player.MaxHealth);
                if (player.OwnedWeapons[(int)WeaponType.Fist])
                    player.PendingWeapon = WeaponType.Fist;
                break;
            case PickupKind.Invulnerability:
                player.InvulnerabilityLeft = Player.InvulnerabilityTics;
                break;
            case PickupKind.RadiationSuit:
                player.RadiationSuitLeft = Player.RadiationSuitTics;
                break;
            case PickupKind.Invisibility:
                player.InvisibilityLeft = Player.InvisibilityTics;
                break;
            case PickupKind.ComputerMap:
                player.HasComputerMap = true;
                break;
            case PickupKind.LightAmp:
                player.LightAmpLeft = Player.LightAmpTics;
                break;
            default:
                return false;
        }

        Say(pickup.Message);
        return true;
    }

    private static bool GiveAmmo(Player player, AmmoType type, int amount)
    {
        var max = player.MaxAmmoOf(type);
        if (player.Ammo[(int)type] >= max)
            return false;

        var hadNone = player.Ammo[(int)type] == 0;
        player.Ammo[(int)type] = Math.Min(max, player.Ammo[(int)type] + amount);
        if (!hadNone || player.Weapon is not (WeaponType.Fist or WeaponType.Pistol))
            return true;

        // Like Doom: picking up ammo for a better weapon while on fist or pistol switches to it.
        var better = type switch
        {
            AmmoType.Bullets => WeaponType.Chaingun,
            AmmoType.Shells => WeaponType.Shotgun,
            AmmoType.Cells => WeaponType.PlasmaGun,
            _ => player.Weapon
        };
        if (better != player.Weapon && player.OwnedWeapons[(int)better])
            player.PendingWeapon = better;
        return true;
    }

    private static void TickPowerUps(Player player)
    {
        if (player.InvulnerabilityLeft > 0)
            player.InvulnerabilityLeft--;
        if (player.RadiationSuitLeft > 0)
            player.RadiationSuitLeft--;
        if (player.InvisibilityLeft > 0)
            player.InvisibilityLeft--;
        if (player.LightAmpLeft > 0)
            player.LightAmpLeft--;
        if (player.DamageCount > 0)
            player.DamageCount--;
        if (player.BonusCount > 0)
            player.BonusCount--;
        if (player.Berserk && player.BerserkFade < int.MaxValue)
            player.BerserkFade++;
    }

    public static float WrapAngle(float angle)
    {
        angle %= MathF.PI * 2;
        return angle < 0 ? angle + MathF.PI * 2 : angle;
    }
}
