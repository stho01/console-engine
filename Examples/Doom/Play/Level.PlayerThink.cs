using System;
using ConsoleEngine.Ecs;
using Doom.Components;
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
        ref var body = ref World.Get<Body>(player.Body);
        ref var momentum = ref World.Get<Momentum>(player.Body);
        if (player.Dead)
        {
            TickDeadPlayer(player, ref body, ref momentum, command);
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
            momentum.X += (cos * command.Forward * WalkThrust + sin * command.Strafe * StrafeThrust) * factor;
            momentum.Y += (sin * command.Forward * WalkThrust - cos * command.Strafe * StrafeThrust) * factor;
        }

        MoveByMomentum(player.Body);
        if (Exited)
            return;

        if (SectorMoved(body))
        {
            CheckPosition(player.Body, body.X, body.Y, out var check);
            body.FloorZ = check.FloorZ;
            body.CeilingZ = check.CeilingZ;
        }

        var previousZ = body.Z;
        if (body.Z < body.FloorZ)
        {
            player.CurrentViewHeight -= body.FloorZ - body.Z;
            body.Z = body.FloorZ;
        }

        ApplyGravity(ref body, ref momentum);
        if (previousZ > body.Z + 8 && body.Z <= body.FloorZ && momentum.Z == 0)
            player.CurrentViewHeight -= MathF.Min(8, (previousZ - body.Z) / 4);

        if (body.Z <= body.FloorZ)
        {
            momentum.X *= Friction;
            momentum.Y *= Friction;
            if (MathF.Abs(momentum.X) < 0.05f && MathF.Abs(momentum.Y) < 0.05f && command.Forward == 0
                && command.Strafe == 0)
            {
                momentum.X = 0;
                momentum.Y = 0;
            }
        }

        UpdateView(player, body, momentum);
        if (command.Use)
            UseLines(player.Body, body);
        TickWeapon(player, command);
        TickSectorSpecial(player, body);
        TickPowerUps(player);
    }

    private void TickDeadPlayer(Player player, ref Body body, ref Momentum momentum, in PlayerCommand command)
    {
        momentum.X = 0;
        momentum.Y = 0;
        if (player.CurrentViewHeight > DeadViewHeight)
            player.CurrentViewHeight = MathF.Max(DeadViewHeight, player.CurrentViewHeight - 1);
        player.ViewZ = body.Z + player.CurrentViewHeight;
        if (player.DamageCount > 0)
            player.DamageCount--;
        if (player.CurrentViewHeight <= DeadViewHeight && (command.Use || command.Fire))
            RestartRequested = true;
    }

    /// <summary>Eye height: recovers from steps and landings, plus a bob that grows with speed.</summary>
    private static void UpdateView(Player player, in Body body, in Momentum momentum)
    {
        var speedSquared = momentum.X * momentum.X + momentum.Y * momentum.Y;
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
    private void UseLines(Entity user, in Body body)
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
            ActivateLine(nearest, user, LineTrigger.Use, nearest.SideOf(body.X, body.Y) > 0);
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
        var shooter = player.Body;
        ref var body = ref World.Get<Body>(shooter);
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
                var target = Hitscan(shooter, body.Angle + (Random.Next(256) - Random.Next(256)) / 255f * 0.05f,
                    info.Range, damage);
                if (target != Entity.Null)
                {
                    ref var targetBody = ref World.Get<Body>(target);
                    body.Angle = AngleTo(body, targetBody.X, targetBody.Y);
                }

                break;
            case WeaponKind.Hitscan:
                for (var i = 0; i < info.Pellets; i++)
                {
                    var accurate = info.FirstShotAccurate && !player.Refiring;
                    var spread = accurate ? 0 : (Random.Next(256) - Random.Next(256)) / 255f * info.Spread;
                    Hitscan(shooter, body.Angle + spread, info.Range, RandomRange(info.DamageMin, info.DamageMax));
                }

                break;
            case WeaponKind.Missile:
                SpawnMissile(shooter, info.Missile, body.Angle, Entity.Null);
                break;
        }

        player.Refiring = true;
        if (info.Type != WeaponType.Fist)
            NoiseAlert(Map.Sectors[body.Sector]);
    }

    //**********************************************************
    //** power-ups
    //**********************************************************

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
