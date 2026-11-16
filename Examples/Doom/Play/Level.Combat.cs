using System;
using ConsoleEngine.Ecs;
using Doom.Components;
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
    /// solid wall or a closed opening is nearer. Walls get a puff, things blood (or a puff). Returns what was hit, or
    /// <see cref="Entity.Null"/>.
    /// </summary>
    public Entity Hitscan(Entity shooter, float angle, float range, int damage)
    {
        ref var shooterBody = ref World.Get<Body>(shooter);
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var sx = shooterBody.X;
        var sy = shooterBody.Y;
        var ex = sx + cos * range;
        var ey = sy + sin * range;
        var shotZ = shooterBody.Z + shooterBody.Height * 0.73f;

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
        var hit = Entity.Null;
        var hitDistance = wallDistance;
        foreach (var chunk in _bodies.GetChunks())
        {
            var entities = chunk.Entities;
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var thing = ref things[i];
                if (entities[i] == shooter || thing.Removed || (thing.Flags & ThingFlags.Shootable) == 0)
                    continue;

                ref var body = ref bodies[i];
                var dx = body.X - sx;
                var dy = body.Y - sy;
                var along = dx * cos + dy * sin;
                if (along <= 0)
                    continue;

                var across = MathF.Abs(-dx * sin + dy * cos);
                if (across > body.Radius)
                    continue;

                var entry = along - MathF.Sqrt(body.Radius * body.Radius - across * across);
                if (entry >= hitDistance)
                    continue;

                hit = entities[i];
                hitDistance = MathF.Max(entry, 0);
            }
        }

        if (hit != Entity.Null)
        {
            ref var hitBody = ref World.Get<Body>(hit);
            var spot = (World.Get<Thing>(hit).Flags & ThingFlags.NoBlood) != 0 ? ThingTypes.BulletPuff : ThingTypes.Blood;
            var z = hitBody.Z + hitBody.Height * 0.5f + RandomRange(-8, 8);
            Enqueue(new SpawnRequest(spot, sx + cos * hitDistance, sy + sin * hitDistance, z));
            Damage(hit, shooter, shooter, damage);
            return hit;
        }

        if (wall == null)
            return Entity.Null;

        if (wall.Special != 0)
            ActivateLine(wall, shooter, LineTrigger.Shoot, wall.SideOf(sx, sy) > 0);

        var back = MathF.Max(0, wallDistance - 4);
        var puffSector = Map.SectorAt(sx + cos * back, sy + sin * back);
        if (puffSector.CeilingFlat != Textures.SkyFlat || shotZ < puffSector.CeilingHeight)
        {
            var puffTop = MathF.Max(puffSector.FloorHeight, puffSector.CeilingHeight - 4);
            var puffZ = Math.Clamp(shotZ, puffSector.FloorHeight, puffTop);
            Enqueue(new SpawnRequest(ThingTypes.BulletPuff, sx + cos * back, sy + sin * back, puffZ));
        }

        return Entity.Null;
    }

    /// <summary>
    /// Deals damage. The player loses health through armor; a monster may flinch and turns on whoever hurt it; at 0
    /// health things die (barrels explode). <paramref name="inflictor"/> (what hit, for knockback) and
    /// <paramref name="source"/> (who is to blame) may be <see cref="Entity.Null"/>.
    /// </summary>
    public void Damage(Entity target, Entity inflictor, Entity source, int damage)
    {
        ref var thing = ref World.Get<Thing>(target);
        if (damage <= 0 || thing.Removed || (thing.Flags & ThingFlags.Shootable) == 0)
            return;

        if (IsPlayer(target))
        {
            DamagePlayer(damage);
            return;
        }

        thing.Health -= damage;
        if (thing.Health <= 0)
        {
            Kill(target, source);
            return;
        }

        var info = ThingInfo.ById(thing.Type);
        var monsterInfo = info.Monster;
        if (monsterInfo == null)
            return;

        ref var monster = ref World.Get<Monster>(target);
        if (source != Entity.Null && source != target && World.IsAlive(source) && !World.Get<Thing>(source).Removed
            && (IsPlayer(source) || (World.Get<Thing>(source).Flags & ThingFlags.Monster) != 0))
            monster.Target = source;
        if (thing.State is ThingState.Idle)
            Wake(target);
        if (Random.Next(256) < monsterInfo.PainChance && thing.State != ThingState.Charge)
        {
            thing.State = ThingState.Pain;
            Animator.Play(ref World.Get<Animation>(target), info, AnimationClip.Pain, 6, false);
        }

        if (inflictor != Entity.Null && (thing.Flags & ThingFlags.Float) == 0)
        {
            ref var body = ref World.Get<Body>(target);
            ref var momentum = ref World.Get<Momentum>(target);
            var push = MathF.Min(damage * 0.12f / MathF.Max(1, info.Health / 100f), 8);
            var angle = AngleTo(World.Get<Body>(inflictor), body.X, body.Y);
            momentum.X += MathF.Cos(angle) * push;
            momentum.Y += MathF.Sin(angle) * push;
        }
    }

    /// <summary>
    /// Damages every shootable thing within <paramref name="damage"/> world units of <paramref name="center"/> that
    /// it can see: full damage at the centre, one point less per unit of distance (from the thing's edge).
    /// </summary>
    public void RadiusDamage(Entity center, Entity source, int damage)
    {
        ref var centerBody = ref World.Get<Body>(center);
        foreach (var chunk in _bodies.GetChunks())
        {
            var entities = chunk.Entities;
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var thing = ref things[i];
                if (entities[i] == center || thing.Removed || (thing.Flags & ThingFlags.Shootable) == 0)
                    continue;

                ref var body = ref bodies[i];
                var distance = MathF.Max(0, Distance(centerBody, body) - body.Radius);
                var dealt = (int)(damage - distance);
                if (dealt <= 0 || !CanSee(centerBody, body))
                    continue;

                Damage(entities[i], center, source, dealt);
            }
        }
    }

    /// <summary>
    /// Launches a projectile from <paramref name="shooter"/> at <paramref name="angle"/>, rising or falling towards
    /// <paramref name="target"/>'s middle when there is one (else <see cref="Entity.Null"/>). It joins the level at
    /// the end of the tic.
    /// </summary>
    public void SpawnMissile(Entity shooter, ThingInfo info, float angle, Entity target)
    {
        ref var shooterBody = ref World.Get<Body>(shooter);
        var z = shooterBody.Z + 32;
        var momentum = new Momentum { X = MathF.Cos(angle) * info.Speed, Y = MathF.Sin(angle) * info.Speed };
        if (target != Entity.Null)
        {
            ref var targetBody = ref World.Get<Body>(target);
            var tics = MathF.Max(1, Distance(shooterBody, targetBody)) / info.Speed;
            momentum.Z = (targetBody.Z + targetBody.Height / 2 - z) / MathF.Max(1, tics);
        }

        Enqueue(new SpawnRequest(info, shooterBody.X, shooterBody.Y, z)
        {
            Angle = angle, Momentum = momentum, Owner = shooter
        });
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

        World.Get<Thing>(player.Body).Flags &= ~(ThingFlags.Shootable | ThingFlags.Solid);
    }

    /// <summary>Starts a thing's death: it stops blocking, plays its death frames, and drops what it carries.</summary>
    private void Kill(Entity target, Entity source)
    {
        ref var thing = ref World.Get<Thing>(target);
        ref var body = ref World.Get<Body>(target);
        ref var animation = ref World.Get<Animation>(target);
        var info = ThingInfo.ById(thing.Type);
        thing.Health = 0;
        thing.Flags &= ~(ThingFlags.Shootable | ThingFlags.Solid | ThingFlags.Float);
        thing.State = ThingState.Dying;
        if (World.Has<Momentum>(target))
        {
            ref var momentum = ref World.Get<Momentum>(target);
            momentum.X = 0;
            momentum.Y = 0;
        }

        if (World.Has<Explosive>(target))
        {
            ref var explosive = ref World.Get<Explosive>(target);
            explosive.PendingDamage = info.SplashDamage;
            explosive.Source = source;
        }

        if (info.DeathSprite != null)
            animation.Sprite = Sprites.IdOf(info.DeathSprite);
        Animator.Play(ref animation, info, AnimationClip.Death, 5, false);
        if (info.Has(ThingFlags.CountKill))
            Stats.Kills++;

        if (info.Has(ThingFlags.Float) && !info.RemoveAfterDeath)
            body.Z = body.FloorZ;

        var drop = info.Monster?.Drops ?? 0;
        if (drop == 0)
            return;

        var dropInfo = ThingTypes.Find(drop);
        if (dropInfo != null)
            Enqueue(new SpawnRequest(dropInfo, body.X, body.Y, float.NaN) { Dropped = true });
    }

    /// <summary>Stops a projectile and plays its explosion; rockets deal their splash damage now.</summary>
    internal void Explode(Entity missile)
    {
        ref var thing = ref World.Get<Thing>(missile);
        ref var momentum = ref World.Get<Momentum>(missile);
        var info = ThingInfo.ById(thing.Type);
        momentum = default;
        thing.Flags &= ~ThingFlags.Missile;
        thing.State = ThingState.Dying;
        Animator.Play(ref World.Get<Animation>(missile), info, AnimationClip.Death, 4, false);
        if (info.SplashDamage > 0)
            RadiusDamage(missile, World.Get<Missile>(missile).Owner, info.SplashDamage);
    }

    /// <summary>A missile flying into the sky above a two-sided line just vanishes.</summary>
    internal bool IsSkyHole(Line line, in Body missile)
    {
        var top = MathF.Min(line.FrontSector.CeilingHeight, line.BackSector.CeilingHeight);
        return missile.Z + missile.Height > top && line.FrontSector.CeilingFlat == Textures.SkyFlat
                                                && line.BackSector.CeilingFlat == Textures.SkyFlat;
    }
}
