using System;
using Doom.Maps;

namespace Doom.Play;

public sealed partial class Level
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Tics each walk and attack frame shows.</summary>
    private const int WalkFrameTics = 4;

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

    //**********************************************************
    //** internals
    //**********************************************************

    private void TickMonster(Actor monster)
    {
        var info = monster.Info.Monster;
        if (info == null)
            return;

        if (monster.MomX != 0 || monster.MomY != 0)
            Knockback(monster);
        if (!monster.Has(ThingFlags.Float))
        {
            if (monster.Sector.Moved)
                SettleOnFloor(monster);
            ApplyGravity(monster);
        }

        switch (monster.State)
        {
            case ActorState.Idle:
                monster.Animate();
                if ((Tic + (monster.GetHashCode() & 0xFFFF)) % LookInterval == 0)
                    LookForPlayer(monster);
                break;
            case ActorState.Chase:
                Chase(monster, info);
                break;
            case ActorState.Attack:
                TickAttack(monster, info);
                break;
            case ActorState.Pain:
                monster.Animate();
                if (monster.AnimationDone)
                    StartChasing(monster);
                break;
            case ActorState.Charge:
                TickCharge(monster, info);
                break;
        }
    }

    /// <summary>A shove from damage: slides the monster and lets friction stop it.</summary>
    private void Knockback(Actor monster)
    {
        if (!TryMove(monster, monster.X + monster.MomX, monster.Y + monster.MomY, out _))
        {
            monster.MomX = 0;
            monster.MomY = 0;
            return;
        }

        monster.MomX *= 0.8f;
        monster.MomY *= 0.8f;
        if (MathF.Abs(monster.MomX) < 0.1f && MathF.Abs(monster.MomY) < 0.1f)
        {
            monster.MomX = 0;
            monster.MomY = 0;
        }
    }

    /// <summary>
    /// An idle monster wakes when it hears a shot in its sector (unless it is an ambusher), or sees the player in
    /// front of it, or anywhere when the player is close.
    /// </summary>
    private void LookForPlayer(Actor monster)
    {
        var body = Player.Body;
        if (Player.Dead)
            return;

        var heard = _heardSound[monster.Sector.Index] && !monster.Ambush;
        if (!heard)
        {
            if (!CanSee(monster, body))
                return;

            var distance = Distance(monster, body);
            if (distance > CloseRange)
            {
                var facing = MathF.Abs(AngleDifference(AngleTo(monster, body.X, body.Y), monster.Angle));
                if (facing > MathF.PI / 2)
                    return;
            }
        }

        monster.Target = body;
        Wake(monster);
    }

    private void Wake(Actor monster)
    {
        if (monster.Target == null && !Player.Dead)
            monster.Target = Player.Body;
        monster.ReactionTics = 8;
        StartChasing(monster);
    }

    private static void StartChasing(Actor monster)
    {
        monster.State = ActorState.Chase;
        monster.Play(monster.Info.Monster.WalkFrames, WalkFrameTics, true);
    }

    /// <summary>
    /// Walks towards the target, picking a new direction when blocked or every so often, and opens doors in its way.
    /// When it can attack and the dice agree, it starts an attack.
    /// </summary>
    private void Chase(Actor monster, MonsterInfo info)
    {
        monster.Animate();
        var target = monster.Target;
        if (target == null || target.Removed || (target.IsPlayer ? Player.Dead : target.Health <= 0))
        {
            monster.Target = null;
            if (!Player.Dead && CanSee(monster, Player.Body))
            {
                monster.Target = Player.Body;
            }
            else
            {
                monster.State = ActorState.Idle;
                monster.Play(monster.Info.Frames, monster.Info.FrameTics, true);
                return;
            }

            target = monster.Target;
        }

        if (monster.ReactionTics > 0)
            monster.ReactionTics--;
        if (monster.AttackCooldown > 0)
            monster.AttackCooldown--;

        if (monster.ReactionTics == 0 && monster.AttackCooldown == 0 && TryStartAttack(monster, info, target))
            return;

        if (--monster.MoveTics <= 0)
            PickDirection(monster, target);

        var speed = info.Speed;
        var nx = monster.X + MathF.Cos(monster.MoveAngle) * speed;
        var ny = monster.Y + MathF.Sin(monster.MoveAngle) * speed;
        if (monster.Has(ThingFlags.Float))
        {
            var wantZ = target.Z + target.Height / 2 - monster.Height / 2;
            monster.Z += Math.Clamp(wantZ - monster.Z, -2f, 2f);
            var highest = MathF.Max(monster.FloorZ, monster.CeilingZ - monster.Height);
            monster.Z = Math.Clamp(monster.Z, monster.FloorZ, highest);
        }

        if (TryMove(monster, nx, ny, out var check))
        {
            monster.Angle = monster.MoveAngle;
            return;
        }

        var blocker = check.BlockingLine;
        if (blocker != null && blocker.Special != 0)
            ActivateLine(blocker, monster, LineTrigger.Use, blocker.SideOf(monster.X, monster.Y) > 0);
        monster.MoveTics = 0;
    }

    /// <summary>
    /// Heads for the target, or the nearest of the other seven directions that is free, now and then at random.
    /// </summary>
    private void PickDirection(Actor monster, Actor target)
    {
        var toward = MathF.Round(AngleTo(monster, target.X, target.Y) / Eighth) * Eighth;
        var speed = monster.Info.Monster.Speed * WalkFrameTics;
        Span<float> tries = stackalloc float[8];
        var count = 0;
        var randomFirst = Random.Next(5) == 0;
        if (randomFirst)
            tries[count++] = Random.Next(8) * Eighth;
        tries[count++] = toward;
        var turn = Random.Next(2) == 0 ? 1 : -1;
        tries[count++] = toward + turn * Eighth;
        tries[count++] = toward - turn * Eighth;
        tries[count++] = toward + turn * 2 * Eighth;
        tries[count++] = toward - turn * 2 * Eighth;
        tries[count++] = toward + turn * 3 * Eighth;
        if (count < tries.Length)
            tries[count++] = toward - turn * 3 * Eighth;

        monster.MoveTics = RandomRange(4, 16);
        for (var i = 0; i < count; i++)
        {
            var angle = tries[i];
            var x = monster.X + MathF.Cos(angle) * speed;
            var y = monster.Y + MathF.Sin(angle) * speed;
            CheckPosition(monster, x, y, out var check);
            if (check.Blocked && check.BlockingLine?.Special == 0)
                continue;
            if (check.BlockingActor != null)
                continue;
            if (!monster.Has(ThingFlags.Float) && !check.Blocked
                && (check.FloorZ - monster.Z > MaxStepHeight || check.FloorZ - check.DropOffZ > MaxStepHeight
                                                             || check.CeilingZ - check.FloorZ < monster.Height))
                continue;

            monster.MoveAngle = angle;
            return;
        }

        monster.MoveAngle = toward + MathF.PI;
    }

    /// <summary>Starts an attack when one fits the range and the dice say yes. Returns true if it did.</summary>
    private bool TryStartAttack(Actor monster, MonsterInfo info, Actor target)
    {
        var distance = Distance(monster, target);
        var meleeReach = Weapons.MeleeRange + target.Radius - 20;
        var canMelee = info.Attack is MonsterAttack.Melee or MonsterAttack.MeleeOrMissile;
        if (canMelee && distance < meleeReach && CanSee(monster, target))
        {
            BeginAttack(monster, info, target);
            return true;
        }

        if (info.Attack == MonsterAttack.Melee)
            return false;

        monster.AttackCooldown = RandomRange(4, 12);
        if (!CanSee(monster, target))
            return false;

        var odds = Math.Clamp((int)(distance / 8) - (canMelee ? 0 : 8), 8, 200);
        if (info.Attack == MonsterAttack.Charge)
            odds = Math.Min(odds, 120);
        if (Random.Next(256) < odds)
            return false;

        BeginAttack(monster, info, target);
        return true;
    }

    private void BeginAttack(Actor monster, MonsterInfo info, Actor target)
    {
        monster.Angle = AngleTo(monster, target.X, target.Y);
        monster.State = ActorState.Attack;
        monster.Play(info.AttackFrames, AttackFrameTics, false);
    }

    /// <summary>Faces the target through the wind-up; the blow, shot or throw comes as the last frame shows.</summary>
    private void TickAttack(Actor monster, MonsterInfo info)
    {
        var target = monster.Target;
        if (target != null && !target.Removed)
            monster.Angle = AngleTo(monster, target.X, target.Y);

        var changed = monster.Animate();
        if (changed && monster.FrameIndex == monster.Frames.Length - 1 && target != null)
            Strike(monster, info, target);

        if (monster.Frames.Length == 1 && monster.FrameTics == AttackFrameTics - 1 && target != null)
            Strike(monster, info, target);

        if (!monster.AnimationDone || monster.State != ActorState.Attack)
            return;

        monster.AttackCooldown = RandomRange(8, 24);
        StartChasing(monster);
    }

    private void Strike(Actor monster, MonsterInfo info, Actor target)
    {
        var distance = Distance(monster, target);
        var inReach = distance < Weapons.MeleeRange + target.Radius - 20 && CanSee(monster, target);
        switch (info.Attack)
        {
            case MonsterAttack.Hitscan:
                var aim = AngleTo(monster, target.X, target.Y);
                for (var i = 0; i < info.Pellets; i++)
                {
                    var spread = (Random.Next(256) - Random.Next(256)) / 255f * (MathF.PI / 8);
                    if (target.IsPlayer && Player.InvisibilityLeft > 0)
                        spread *= 2;
                    Hitscan(monster, aim + spread, 2048, RandomRange(info.DamageMin, info.DamageMax));
                }

                break;
            case MonsterAttack.Melee:
                if (inReach)
                    Damage(target, monster, monster, RandomRange(info.MeleeDamageMin, info.MeleeDamageMax));
                break;
            case MonsterAttack.MeleeOrMissile:
                if (inReach)
                    Damage(target, monster, monster, RandomRange(info.MeleeDamageMin, info.MeleeDamageMax));
                else if (info.Missile != null)
                    SpawnMissile(monster, info.Missile, AngleTo(monster, target.X, target.Y), target);
                break;
            case MonsterAttack.Charge:
                monster.State = ActorState.Charge;
                monster.MoveTics = ChargeTics;
                var angle = AngleTo(monster, target.X, target.Y);
                monster.MomX = MathF.Cos(angle) * ChargeSpeed;
                monster.MomY = MathF.Sin(angle) * ChargeSpeed;
                var steps = MathF.Max(1, distance / ChargeSpeed);
                monster.MomZ = (target.Z + target.Height / 2 - (monster.Z + monster.Height / 2)) / steps;
                break;
        }
    }

    /// <summary>A lost soul flies straight on until it hits something (biting it) or the charge runs out.</summary>
    private void TickCharge(Actor monster, MonsterInfo info)
    {
        monster.Animate();
        monster.Z = Math.Clamp(monster.Z + monster.MomZ, monster.FloorZ,
            MathF.Max(monster.FloorZ, monster.CeilingZ - monster.Height));
        var stepX = monster.MomX / 2;
        var stepY = monster.MomY / 2;
        for (var i = 0; i < 2; i++)
        {
            if (TryMove(monster, monster.X + stepX, monster.Y + stepY, out var check))
                continue;

            if (check.BlockingActor != null && check.BlockingActor.Has(ThingFlags.Shootable))
                Damage(check.BlockingActor, monster, monster, RandomRange(info.DamageMin, info.DamageMax));
            EndCharge(monster);
            return;
        }

        if (--monster.MoveTics <= 0)
            EndCharge(monster);
    }

    private static void EndCharge(Actor monster)
    {
        monster.MomX = 0;
        monster.MomY = 0;
        monster.MomZ = 0;
        monster.AttackCooldown = 20;
        if (monster.State == ActorState.Charge)
            StartChasing(monster);
    }

    /// <summary>
    /// A shot wakes the monsters in every sector the sound reaches from <paramref name="sector"/>: through open
    /// two-sided lines, and through at most one line flagged as blocking sound.
    /// </summary>
    private void NoiseAlert(Sector sector)
    {
        _soundQueue.Clear();
        Array.Fill(_soundBlocks, int.MaxValue);
        _soundQueue.Enqueue((sector, 0));
        _soundBlocks[sector.Index] = 0;
        while (_soundQueue.Count > 0)
        {
            var (current, blocks) = _soundQueue.Dequeue();
            _heardSound[current.Index] = true;
            foreach (var line in current.Lines)
            {
                if (line.Back == null)
                    continue;

                var front = line.FrontSector;
                var back = line.BackSector;
                if (MathF.Min(front.CeilingHeight, back.CeilingHeight) <= MathF.Max(front.FloorHeight, back.FloorHeight))
                    continue;

                var next = front == current ? back : front;
                var nextBlocks = blocks + ((line.Flags & LineFlags.BlockSound) != 0 ? 1 : 0);
                if (nextBlocks > 1 || _soundBlocks[next.Index] <= nextBlocks)
                    continue;

                _soundBlocks[next.Index] = nextBlocks;
                _soundQueue.Enqueue((next, nextBlocks));
            }
        }
    }

    /// <summary>The signed difference a - b, wrapped to [-π, π].</summary>
    public static float AngleDifference(float a, float b)
    {
        var difference = (a - b) % (2 * MathF.PI);
        if (difference > MathF.PI)
            difference -= 2 * MathF.PI;
        if (difference < -MathF.PI)
            difference += 2 * MathF.PI;
        return difference;
    }
}
