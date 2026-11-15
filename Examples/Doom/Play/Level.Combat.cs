using System;
using Doom.Maps;

namespace Doom.Play;

public sealed partial class Level
{
    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Fires an instant shot from <paramref name="shooter"/> along <paramref name="angle"/>. Aiming is 2D, like
    /// Doom's auto-aim: the shot hits the nearest shootable thing whose footprint the line passes through, unless a
    /// solid wall or a closed opening is nearer. Walls get a puff, things blood (or a puff). Returns what was hit.
    /// </summary>
    public Actor Hitscan(Actor shooter, float angle, float range, int damage)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var sx = shooter.X;
        var sy = shooter.Y;
        var ex = sx + cos * range;
        var ey = sy + sin * range;
        var shotZ = shooter.Z + shooter.Height * 0.73f;

        var wallFraction = 1f;
        Line wall = null;
        var lines = Map.Lines;
        var minX = MathF.Min(sx, ex);
        var maxX = MathF.Max(sx, ex);
        var minY = MathF.Min(sy, ey);
        var maxY = MathF.Max(sy, ey);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.MaxX < minX || line.MinX > maxX || line.MaxY < minY || line.MinY > maxY)
                continue;
            if (!SegmentsCross(sx, sy, ex, ey, line, out var fraction) || fraction >= wallFraction)
                continue;

            if (line.Back != null)
            {
                var openTop = MathF.Min(line.FrontSector.CeilingHeight, line.BackSector.CeilingHeight);
                var openBottom = MathF.Max(line.FrontSector.FloorHeight, line.BackSector.FloorHeight);
                if (openTop - openBottom > 0)
                    continue;
            }

            wallFraction = fraction;
            wall = line;
        }

        var wallDistance = wallFraction * range;
        Actor hit = null;
        var hitDistance = wallDistance;
        for (var i = 0; i < _actors.Count; i++)
        {
            var target = _actors[i];
            if (target == shooter || target.Removed || !target.Has(ThingFlags.Shootable))
                continue;

            var dx = target.X - sx;
            var dy = target.Y - sy;
            var along = dx * cos + dy * sin;
            if (along <= 0)
                continue;

            var across = MathF.Abs(-dx * sin + dy * cos);
            if (across > target.Radius)
                continue;

            var entry = along - MathF.Sqrt(target.Radius * target.Radius - across * across);
            if (entry >= hitDistance)
                continue;

            hit = target;
            hitDistance = MathF.Max(entry, 0);
        }

        if (hit != null)
        {
            var spot = hit.Has(ThingFlags.NoBlood) ? ThingTypes.BulletPuff : ThingTypes.Blood;
            var z = hit.Z + hit.Height * 0.5f + RandomRange(-8, 8);
            Spawn(spot, sx + cos * hitDistance, sy + sin * hitDistance, z);
            Damage(hit, shooter, shooter, damage);
            return hit;
        }

        if (wall == null)
            return null;

        if (wall.Special != 0)
            ActivateLine(wall, shooter, LineTrigger.Shoot, wall.SideOf(sx, sy) > 0);

        var back = MathF.Max(0, wallDistance - 4);
        var puffSector = Map.SectorAt(sx + cos * back, sy + sin * back);
        if (puffSector.CeilingFlat != Textures.SkyFlat || shotZ < puffSector.CeilingHeight)
        {
            var puffTop = MathF.Max(puffSector.FloorHeight, puffSector.CeilingHeight - 4);
            var puffZ = Math.Clamp(shotZ, puffSector.FloorHeight, puffTop);
            Spawn(ThingTypes.BulletPuff, sx + cos * back, sy + sin * back, puffZ);
        }

        return null;
    }

    /// <summary>
    /// Deals damage. The player loses health through armor; a monster may flinch and turns on whoever hurt it; at 0
    /// health things die (barrels explode).
    /// </summary>
    public void Damage(Actor target, Actor inflictor, Actor source, int damage)
    {
        if (damage <= 0 || target.Removed || !target.Has(ThingFlags.Shootable))
            return;

        if (target.IsPlayer)
        {
            DamagePlayer(damage);
            return;
        }

        target.Health -= damage;
        if (target.Health <= 0)
        {
            Kill(target, source);
            return;
        }

        var monster = target.Info.Monster;
        if (monster == null)
            return;

        if (source != null && source != target && !source.Removed && (source.IsPlayer || source.Has(ThingFlags.Monster)))
            target.Target = source;
        if (target.State is ActorState.Idle)
            Wake(target);
        if (Random.Next(256) < monster.PainChance && target.State != ActorState.Charge)
        {
            target.State = ActorState.Pain;
            target.Play(monster.PainFrames, 6, false);
        }

        if (inflictor != null && !target.Has(ThingFlags.Float))
        {
            var push = damage * 0.12f / MathF.Max(1, target.Info.Health / 100f);
            var angle = AngleTo(inflictor, target.X, target.Y);
            target.MomX += MathF.Cos(angle) * MathF.Min(push, 8);
            target.MomY += MathF.Sin(angle) * MathF.Min(push, 8);
        }
    }

    /// <summary>
    /// Damages every shootable thing within <paramref name="damage"/> world units of <paramref name="center"/> that
    /// it can see: full damage at the centre, one point less per unit of distance (from the thing's edge).
    /// </summary>
    public void RadiusDamage(Actor center, Actor source, int damage)
    {
        for (var i = 0; i < _actors.Count; i++)
        {
            var target = _actors[i];
            if (target == center || target.Removed || !target.Has(ThingFlags.Shootable))
                continue;

            var distance = MathF.Max(0, Distance(center, target) - target.Radius);
            var dealt = (int)(damage - distance);
            if (dealt <= 0 || !CanSee(center, target))
                continue;

            Damage(target, center, source, dealt);
        }
    }

    /// <summary>Launches a projectile from <paramref name="shooter"/> at <paramref name="angle"/>, rising or falling
    /// towards <paramref name="target"/>'s middle when there is one.</summary>
    public Actor SpawnMissile(Actor shooter, ThingInfo info, float angle, Actor target)
    {
        var z = shooter.Z + 32;
        var missile = Spawn(info, shooter.X, shooter.Y, z);
        missile.Owner = shooter;
        missile.Target = shooter;
        missile.Angle = angle;
        missile.MomX = MathF.Cos(angle) * info.Speed;
        missile.MomY = MathF.Sin(angle) * info.Speed;
        if (target != null)
        {
            var distance = MathF.Max(1, Distance(shooter, target)) / info.Speed;
            missile.MomZ = (target.Z + target.Height / 2 - z) / MathF.Max(1, distance);
        }

        return missile;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DamagePlayer(int damage)
    {
        var player = Player;
        if (player.Dead || player.InvulnerabilityLeft > 0)
            return;

        if (player.ArmorClass > 0)
        {
            var saved = player.ArmorClass == 1 ? damage / 3 : damage / 2;
            if (saved >= player.Armor)
            {
                saved = player.Armor;
                player.ArmorClass = 0;
            }

            player.Armor -= saved;
            damage -= saved;
        }

        player.Health = Math.Max(0, player.Health - damage);
        player.DamageCount = Math.Min(100, player.DamageCount + damage);
        if (!player.Dead)
            return;

        player.Body.Flags &= ~(ThingFlags.Shootable | ThingFlags.Solid);
    }

    private void Kill(Actor target, Actor source)
    {
        target.Health = 0;
        target.Flags &= ~(ThingFlags.Shootable | ThingFlags.Solid | ThingFlags.Float);
        target.State = ActorState.Dying;
        target.MomX = 0;
        target.MomY = 0;
        target.Target = source;
        if (target.Info.DeathSprite != null)
            target.Sprite = Sprites.Find(target.Info.DeathSprite);
        target.Play(target.Info.DeathFrames, 5, false);
        if (target.Info.SplashDamage > 0)
            target.PendingSplash = target.Info.SplashDamage;
        if (target.Info.Has(ThingFlags.CountKill))
            Stats.Kills++;

        if (target.Info.Has(ThingFlags.Float) && !target.Info.RemoveAfterDeath)
            target.Z = target.FloorZ;

        var drop = target.Info.Monster?.Drops ?? 0;
        if (drop == 0)
            return;

        var info = ThingTypes.Find(drop);
        if (info == null)
            return;

        var dropped = Spawn(info, target.X, target.Y, float.NaN);
        dropped.Dropped = true;
    }

    /// <summary>Flies a projectile one tic: it explodes on a wall, the floor or ceiling, or a thing it hits.</summary>
    private void TickMissile(Actor missile)
    {
        missile.Animate();
        var steps = (int)MathF.Ceiling(MathF.Max(MathF.Abs(missile.MomX), MathF.Abs(missile.MomY)) / MaxStep);
        steps = Math.Max(1, steps);
        var stepX = missile.MomX / steps;
        var stepY = missile.MomY / steps;
        var stepZ = missile.MomZ / steps;
        for (var i = 0; i < steps; i++)
        {
            missile.Z += stepZ;
            if (!TryMove(missile, missile.X + stepX, missile.Y + stepY, out var check))
            {
                if (check.BlockingActor != null)
                    Damage(check.BlockingActor, missile, missile.Owner, RandomRange(missile.Info.DamageMin,
                        missile.Info.DamageMax));
                else if (check.BlockingLine is { Back: not null } line && IsSkyHole(line, missile))
                {
                    missile.Removed = true;
                    return;
                }

                Explode(missile);
                return;
            }

            if (missile.Z <= missile.FloorZ || missile.Z + missile.Height >= missile.CeilingZ)
            {
                if (missile.Z + missile.Height >= missile.CeilingZ && missile.Sector.CeilingFlat == Textures.SkyFlat)
                {
                    missile.Removed = true;
                    return;
                }

                Explode(missile);
                return;
            }
        }
    }

    /// <summary>A missile flying into the sky above a two-sided line just vanishes.</summary>
    private bool IsSkyHole(Line line, Actor missile)
    {
        var top = MathF.Min(line.FrontSector.CeilingHeight, line.BackSector.CeilingHeight);
        return missile.Z + missile.Height > top && line.FrontSector.CeilingFlat == Textures.SkyFlat
                                                && line.BackSector.CeilingFlat == Textures.SkyFlat;
    }

    private void Explode(Actor missile)
    {
        missile.MomX = 0;
        missile.MomY = 0;
        missile.MomZ = 0;
        missile.Flags &= ~ThingFlags.Missile;
        missile.State = ActorState.Dying;
        missile.Play(missile.Info.DeathFrames, 4, false);
        if (missile.Info.SplashDamage > 0)
            RadiusDamage(missile, missile.Owner, missile.Info.SplashDamage);
    }
}
