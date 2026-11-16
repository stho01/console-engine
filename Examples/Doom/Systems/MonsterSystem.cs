using System;
using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Maps;
using Doom.Play;

namespace Doom.Systems;

/// <summary>
/// The monster AI, one tic per update: idle monsters look and listen for the player; awake ones chase their target,
/// attack when in reach and the dice agree, flinch, and lost souls charge. Dying and dead monsters are left to
/// <see cref="DyingSystem"/> and <see cref="StaticThingSystem"/>.
/// </summary>
public sealed class MonsterSystem(Level level) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int AttackFrameTics = 8;

    /// <summary>Tics between an idle monster's looks around.</summary>
    private const int LookInterval = 4;

    /// <summary>Distance at which a monster notices the player even from behind.</summary>
    private const float CloseRange = 64;

    /// <summary>A lost soul's charge: speed (units per tic) and how long it lasts.</summary>
    private const float ChargeSpeed = 20;

    private const int ChargeTics = 24;

    /// <summary>Eight compass directions, radians.</summary>
    private const float Eighth = MathF.PI / 4;

    private Query _monsters;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _monsters = World.Query(Query.With<Thing, Body, Animation>().With<Momentum, Monster>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _monsters.GetChunks())
        {
            var entities = chunk.Entities;
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            var animations = chunk.GetSpan<Animation>();
            var momenta = chunk.GetSpan<Momentum>();
            var monsters = chunk.GetSpan<Monster>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (things[i].State is ThingState.Dying or ThingState.Dead || things[i].Removed)
                    continue;

                Think(new Mind(entities[i], ref things[i], ref bodies[i], ref animations[i], ref momenta[i],
                    ref monsters[i]));
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>One monster's components, so the AI steps don't pass six refs each.</summary>
    private readonly ref struct Mind
    {
        public readonly Entity Entity;
        public readonly ref Thing Thing;
        public readonly ref Body Body;
        public readonly ref Animation Animation;
        public readonly ref Momentum Momentum;
        public readonly ref Monster Monster;

        public Mind(Entity entity, ref Thing thing, ref Body body, ref Animation animation, ref Momentum momentum,
            ref Monster monster)
        {
            Entity = entity;
            Thing = ref thing;
            Body = ref body;
            Animation = ref animation;
            Momentum = ref momentum;
            Monster = ref monster;
        }

        public ThingInfo Info => ThingInfo.ById(Thing.Type);
        public bool Floats => (Thing.Flags & ThingFlags.Float) != 0;
    }

    private void Think(in Mind mind)
    {
        var info = mind.Info;
        if (mind.Momentum.X != 0 || mind.Momentum.Y != 0)
            Knockback(mind);
        if (!mind.Floats)
        {
            if (level.SectorMoved(mind.Body))
                level.SettleOnFloor(mind.Entity);
            Level.ApplyGravity(ref mind.Body, ref mind.Momentum);
        }

        switch (mind.Thing.State)
        {
            case ThingState.Idle:
                Animator.Advance(ref mind.Animation, info);
                if ((level.Tic + (mind.Entity.Id & 0xFFFF)) % LookInterval == 0)
                    LookForPlayer(mind);
                break;
            case ThingState.Chase:
                Chase(mind, info);
                break;
            case ThingState.Attack:
                TickAttack(mind, info);
                break;
            case ThingState.Pain:
                Animator.Advance(ref mind.Animation, info);
                if (mind.Animation.Done)
                    Level.StartChasing(ref mind.Thing, ref mind.Animation);
                break;
            case ThingState.Charge:
                TickCharge(mind, info.Monster);
                break;
        }
    }

    /// <summary>A shove from damage: slides the monster and lets friction stop it.</summary>
    private void Knockback(in Mind mind)
    {
        ref var momentum = ref mind.Momentum;
        if (!level.TryMove(mind.Entity, mind.Body.X + momentum.X, mind.Body.Y + momentum.Y, out _))
        {
            momentum.X = 0;
            momentum.Y = 0;
            return;
        }

        momentum.X *= 0.8f;
        momentum.Y *= 0.8f;
        if (MathF.Abs(momentum.X) < 0.1f && MathF.Abs(momentum.Y) < 0.1f)
        {
            momentum.X = 0;
            momentum.Y = 0;
        }
    }

    /// <summary>
    /// An idle monster wakes when it hears a shot in its sector (unless it is an ambusher), or sees the player in
    /// front of it, or anywhere when the player is close.
    /// </summary>
    private void LookForPlayer(in Mind mind)
    {
        if (level.Player.Dead)
            return;

        ref var player = ref level.PlayerBody;
        var heard = level.HeardSound(mind.Body.Sector) && !mind.Thing.Ambush;
        if (!heard)
        {
            if (!level.CanSee(mind.Body, player))
                return;

            if (Level.Distance(mind.Body, player) > CloseRange)
            {
                var facing = Level.AngleDifference(Level.AngleTo(mind.Body, player.X, player.Y), mind.Body.Angle);
                if (MathF.Abs(facing) > MathF.PI / 2)
                    return;
            }
        }

        mind.Monster.Target = level.Player.Body;
        level.Wake(mind.Entity);
    }

    /// <summary>
    /// Walks towards the target, picking a new direction when blocked or every so often, and opens doors in its way.
    /// When it can attack and the dice agree, it starts an attack.
    /// </summary>
    private void Chase(in Mind mind, ThingInfo info)
    {
        Animator.Advance(ref mind.Animation, info);
        ref var monster = ref mind.Monster;
        if (!HasLiveTarget(monster.Target))
        {
            monster.Target = Entity.Null;
            if (!level.Player.Dead && level.CanSee(mind.Body, level.PlayerBody))
            {
                monster.Target = level.Player.Body;
            }
            else
            {
                mind.Thing.State = ThingState.Idle;
                Animator.Play(ref mind.Animation, info, AnimationClip.Idle, info.FrameTics, true);
                return;
            }
        }

        ref var target = ref World.Get<Body>(monster.Target);
        if (monster.ReactionTics > 0)
            monster.ReactionTics--;
        if (monster.AttackCooldown > 0)
            monster.AttackCooldown--;

        if (monster.ReactionTics == 0 && monster.AttackCooldown == 0 && TryStartAttack(mind, info.Monster, target))
            return;

        if (--monster.MoveTics <= 0)
            PickDirection(mind, info.Monster, target);

        ref var body = ref mind.Body;
        var speed = info.Monster.Speed;
        var nx = body.X + MathF.Cos(monster.MoveAngle) * speed;
        var ny = body.Y + MathF.Sin(monster.MoveAngle) * speed;
        if (mind.Floats)
        {
            var wantZ = target.Z + target.Height / 2 - body.Height / 2;
            body.Z += Math.Clamp(wantZ - body.Z, -2f, 2f);
            var highest = MathF.Max(body.FloorZ, body.CeilingZ - body.Height);
            body.Z = Math.Clamp(body.Z, body.FloorZ, highest);
        }

        if (level.TryMove(mind.Entity, nx, ny, out var check))
        {
            body.Angle = monster.MoveAngle;
            return;
        }

        var blocker = check.BlockingLine;
        if (blocker != null && blocker.Special != 0)
            level.ActivateLine(blocker, mind.Entity, LineTrigger.Use, blocker.SideOf(body.X, body.Y) > 0);
        monster.MoveTics = 0;
    }

    /// <summary>Whether <paramref name="target"/> is still worth chasing: in play and alive.</summary>
    private bool HasLiveTarget(Entity target)
    {
        if (target == Entity.Null || !World.IsAlive(target))
            return false;
        if (level.IsPlayer(target))
            return !level.Player.Dead;

        ref var thing = ref World.Get<Thing>(target);
        return !thing.Removed && thing.Health > 0;
    }

    /// <summary>
    /// Heads for the target, or the nearest of the other seven directions that is free, now and then at random.
    /// </summary>
    private void PickDirection(in Mind mind, MonsterInfo info, in Body target)
    {
        var random = level.Random;
        ref var body = ref mind.Body;
        var toward = MathF.Round(Level.AngleTo(body, target.X, target.Y) / Eighth) * Eighth;
        var speed = info.Speed * Level.WalkFrameTics;
        Span<float> tries = stackalloc float[8];
        var count = 0;
        if (random.Next(5) == 0)
            tries[count++] = random.Next(8) * Eighth;
        tries[count++] = toward;
        var turn = random.Next(2) == 0 ? 1 : -1;
        tries[count++] = toward + turn * Eighth;
        tries[count++] = toward - turn * Eighth;
        tries[count++] = toward + turn * 2 * Eighth;
        tries[count++] = toward - turn * 2 * Eighth;
        tries[count++] = toward + turn * 3 * Eighth;
        if (count < tries.Length)
            tries[count++] = toward - turn * 3 * Eighth;

        mind.Monster.MoveTics = level.RandomRange(4, 16);
        for (var i = 0; i < count; i++)
        {
            var angle = tries[i];
            var x = body.X + MathF.Cos(angle) * speed;
            var y = body.Y + MathF.Sin(angle) * speed;
            level.CheckPosition(mind.Entity, x, y, out var check);
            if (check.Blocked && check.BlockingLine?.Special == 0)
                continue;
            if (check.BlockingThing != Entity.Null)
                continue;
            if (!mind.Floats && !check.Blocked
                && (check.FloorZ - body.Z > Level.MaxStepHeight
                    || check.FloorZ - check.DropOffZ > Level.MaxStepHeight
                    || check.CeilingZ - check.FloorZ < body.Height))
                continue;

            mind.Monster.MoveAngle = angle;
            return;
        }

        mind.Monster.MoveAngle = toward + MathF.PI;
    }

    /// <summary>Starts an attack when one fits the range and the dice say yes. Returns true if it did.</summary>
    private bool TryStartAttack(in Mind mind, MonsterInfo info, in Body target)
    {
        var distance = Level.Distance(mind.Body, target);
        var meleeReach = Weapons.MeleeRange + target.Radius - 20;
        var canMelee = info.Attack is MonsterAttack.Melee or MonsterAttack.MeleeOrMissile;
        if (canMelee && distance < meleeReach && level.CanSee(mind.Body, target))
        {
            BeginAttack(mind, target);
            return true;
        }

        if (info.Attack == MonsterAttack.Melee)
            return false;

        mind.Monster.AttackCooldown = level.RandomRange(4, 12);
        if (!level.CanSee(mind.Body, target))
            return false;

        var odds = Math.Clamp((int)(distance / 8) - (canMelee ? 0 : 8), 8, 200);
        if (info.Attack == MonsterAttack.Charge)
            odds = Math.Min(odds, 120);
        if (level.Random.Next(256) < odds)
            return false;

        BeginAttack(mind, target);
        return true;
    }

    private static void BeginAttack(in Mind mind, in Body target)
    {
        mind.Body.Angle = Level.AngleTo(mind.Body, target.X, target.Y);
        mind.Thing.State = ThingState.Attack;
        Animator.Play(ref mind.Animation, mind.Info, AnimationClip.Attack, AttackFrameTics, false);
    }

    /// <summary>Faces the target through the wind-up; the blow, shot or throw comes as the last frame shows.</summary>
    private void TickAttack(in Mind mind, ThingInfo info)
    {
        var target = mind.Monster.Target;
        var hasTarget = target != Entity.Null && World.IsAlive(target) && !World.Get<Thing>(target).Removed;
        if (hasTarget)
        {
            ref var targetBody = ref World.Get<Body>(target);
            mind.Body.Angle = Level.AngleTo(mind.Body, targetBody.X, targetBody.Y);
        }

        ref var animation = ref mind.Animation;
        var changed = Animator.Advance(ref animation, info);
        if (hasTarget && changed && animation.FrameIndex == animation.FrameCount - 1)
            Strike(mind, info.Monster, target);
        if (hasTarget && animation.FrameCount == 1 && animation.TicsLeft == AttackFrameTics - 1)
            Strike(mind, info.Monster, target);

        if (!animation.Done || mind.Thing.State != ThingState.Attack)
            return;

        mind.Monster.AttackCooldown = level.RandomRange(8, 24);
        Level.StartChasing(ref mind.Thing, ref animation);
    }

    private void Strike(in Mind mind, MonsterInfo info, Entity target)
    {
        ref var body = ref mind.Body;
        ref var targetBody = ref World.Get<Body>(target);
        var distance = Level.Distance(body, targetBody);
        var inReach = distance < Weapons.MeleeRange + targetBody.Radius - 20 && level.CanSee(body, targetBody);
        var aim = Level.AngleTo(body, targetBody.X, targetBody.Y);
        switch (info.Attack)
        {
            case MonsterAttack.Hitscan:
                var random = level.Random;
                for (var i = 0; i < info.Pellets; i++)
                {
                    var spread = (random.Next(256) - random.Next(256)) / 255f * (MathF.PI / 8);
                    if (level.IsPlayer(target) && level.Player.InvisibilityLeft > 0)
                        spread *= 2;
                    level.Hitscan(mind.Entity, aim + spread, 2048, level.RandomRange(info.DamageMin, info.DamageMax));
                }

                break;
            case MonsterAttack.Melee:
                if (inReach)
                    level.Damage(target, mind.Entity, mind.Entity,
                        level.RandomRange(info.MeleeDamageMin, info.MeleeDamageMax));
                break;
            case MonsterAttack.MeleeOrMissile:
                if (inReach)
                    level.Damage(target, mind.Entity, mind.Entity,
                        level.RandomRange(info.MeleeDamageMin, info.MeleeDamageMax));
                else if (info.Missile != null)
                    level.SpawnMissile(mind.Entity, info.Missile, aim, target);
                break;
            case MonsterAttack.Charge:
                mind.Thing.State = ThingState.Charge;
                mind.Monster.MoveTics = ChargeTics;
                mind.Momentum.X = MathF.Cos(aim) * ChargeSpeed;
                mind.Momentum.Y = MathF.Sin(aim) * ChargeSpeed;
                var steps = MathF.Max(1, distance / ChargeSpeed);
                mind.Momentum.Z = (targetBody.Z + targetBody.Height / 2 - (body.Z + body.Height / 2)) / steps;
                break;
        }
    }

    /// <summary>A lost soul flies straight on until it hits something (biting it) or the charge runs out.</summary>
    private void TickCharge(in Mind mind, MonsterInfo info)
    {
        Animator.Advance(ref mind.Animation, mind.Info);
        ref var body = ref mind.Body;
        body.Z = Math.Clamp(body.Z + mind.Momentum.Z, body.FloorZ, MathF.Max(body.FloorZ, body.CeilingZ - body.Height));
        var stepX = mind.Momentum.X / 2;
        var stepY = mind.Momentum.Y / 2;
        for (var i = 0; i < 2; i++)
        {
            if (level.TryMove(mind.Entity, body.X + stepX, body.Y + stepY, out var check))
                continue;

            var hit = check.BlockingThing;
            if (hit != Entity.Null && (World.Get<Thing>(hit).Flags & ThingFlags.Shootable) != 0)
                level.Damage(hit, mind.Entity, mind.Entity, level.RandomRange(info.DamageMin, info.DamageMax));
            EndCharge(mind);
            return;
        }

        if (--mind.Monster.MoveTics <= 0)
            EndCharge(mind);
    }

    private static void EndCharge(in Mind mind)
    {
        mind.Momentum = default;
        mind.Monster.AttackCooldown = 20;
        if (mind.Thing.State == ThingState.Charge)
            Level.StartChasing(ref mind.Thing, ref mind.Animation);
    }
}
