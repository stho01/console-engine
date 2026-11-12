using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Physics;
using Topple.Components;
using Topple.Effects;

namespace Topple.Systems;

/// <summary>
/// The catapult's controls: UP/DOWN (or W/S) aim, 1/2 pick stone or bomb, hold SPACE to charge and let go to throw. The
/// shot is created through the command buffer, so the physics sees it from the next fixed step.
/// </summary>
public sealed class CatapultSystem(Catapult catapult, Round round, EffectSpawner effects) : EntitySystem
{
    public const float ReloadSeconds = .6f;

    protected override void OnUpdate(float deltaSeconds)
    {
        var input = Input.Instance;
        catapult.Tick(deltaSeconds);
        if (round.Phase != RoundPhase.Playing)
        {
            catapult.Cancel();
            return;
        }

        var turn = 0f;
        if (input.GetKey(Key.UP).Held || input.GetKey(Key.W).Held) turn += 1f;
        if (input.GetKey(Key.DOWN).Held || input.GetKey(Key.S).Held) turn -= 1f;
        catapult.Turn(turn, deltaSeconds);

        if (input.GetKey(Key.K1).Pressed) catapult.Ammo = ShotKind.Stone;
        if (input.GetKey(Key.K2).Pressed) catapult.Ammo = ShotKind.Bomb;
        if (!round.HasAmmo(catapult.Ammo))
            catapult.Ammo = catapult.Ammo == ShotKind.Stone ? ShotKind.Bomb : ShotKind.Stone;

        var space = input.GetKey(Key.SPACE);
        if (catapult.IsCharging && !space.Held)
        {
            var kind = catapult.Ammo;
            var muzzle = catapult.Muzzle;
            var velocity = catapult.Release(ReloadSeconds);
            if (round.TryUseShot(kind))
            {
                BodyFactory.CreateShot(Commands, kind, muzzle, velocity);
                effects.Dust(Commands, muzzle, ConsoleColor.DarkYellow, 3);
            }
        }
        else if (space.Held && catapult.Cooldown <= 0f && round.HasAmmo(catapult.Ammo))
        {
            catapult.ChargeUp(deltaSeconds);
        }
    }
}

/// <summary>Removes splinters and sparks when their time is up.</summary>
public sealed class DebrisSystem : EntitySystem
{
    private Query _debris;

    protected override void OnCreate() => _debris = World.Query(Query.With<Debris>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _debris.GetChunks())
        {
            var debris = chunk.GetSpan<Debris>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                debris[i].SecondsLeft -= deltaSeconds;
                if (debris[i].SecondsLeft <= 0f)
                    Commands.Destroy(entities[i]);
            }
        }
    }
}

/// <summary>
/// Feeds the <see cref="Round"/> what is left in the world (crystals, shots, blocks still awake) and handles the keys
/// between attempts: R restarts the level, ENTER goes on after a win.
/// </summary>
public sealed class RoundSystem(Round round, ToppleGame game) : EntitySystem
{
    private Query _crystals;
    private Query _shots;
    private Query _blocks;

    protected override void OnCreate()
    {
        _crystals = World.Query(Query.With<Crystal>());
        _shots = World.Query(Query.With<Shot>());
        _blocks = World.Query(Query.With<Block, RigidBody>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        round.Update(deltaSeconds, _crystals.Count, _shots.Count, CountAwakeBlocks());

        var input = Input.Instance;
        if (input.GetKey(Key.R).Pressed)
        {
            game.LoadLevel();
        }
        else if (round.Phase == RoundPhase.Won && input.GetKey(Key.ENTER).Pressed)
        {
            game.Campaign.CompleteLevel(round.FinalScore);
            game.LoadLevel();
        }
    }

    private int CountAwakeBlocks()
    {
        var awake = 0;
        foreach (var chunk in _blocks.GetChunks())
        {
            var bodies = chunk.GetSpan<RigidBody>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (!bodies[i].IsSleeping)
                    awake++;
            }
        }

        return awake;
    }
}

/// <summary>Ages and moves puffs (they slow down and smoke rises), and removes them when their time is up.</summary>
public sealed class PuffSystem : EntitySystem
{
    private const float Drag = 2.5f;
    private const float SmokeRise = 2f;

    private Query _puffs;

    protected override void OnCreate() => _puffs = World.Query(Query.With<Puff, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var slowdown = 1f / (1f + Drag * deltaSeconds);
        foreach (var chunk in _puffs.GetChunks())
        {
            var puffs = chunk.GetSpan<Puff>();
            var positions = chunk.GetSpan<Position>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var puff = ref puffs[i];
                puff.Age += deltaSeconds;
                if (puff.Age >= puff.Life)
                {
                    Commands.Destroy(entities[i]);
                    continue;
                }

                if (puff.Kind is PuffKind.Smoke or PuffKind.Flame)
                    puff.Velocity.Y -= SmokeRise * deltaSeconds;
                puff.Velocity *= slowdown;
                positions[i].Value += puff.Velocity * deltaSeconds;
            }
        }
    }
}

/// <summary>Ages fireballs and removes them when they have burnt out.</summary>
public sealed class BurstSystem : EntitySystem
{
    private Query _bursts;

    protected override void OnCreate() => _bursts = World.Query(Query.With<Burst>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _bursts.GetChunks())
        {
            var bursts = chunk.GetSpan<Burst>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                bursts[i].Age += deltaSeconds;
                if (bursts[i].Age >= bursts[i].Life)
                    Commands.Destroy(entities[i]);
            }
        }
    }
}

/// <summary>Leaves a puff behind every flying shot at a fixed rate: smoke after stones, sparks after bombs.</summary>
public sealed class TrailSystem(EffectSpawner effects) : EntitySystem
{
    public const float TrailInterval = .035f;
    private const float MinSpeed = 6f;

    private Query _shots;

    protected override void OnCreate() => _shots = World.Query(Query.With<Shot, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _shots.GetChunks())
        {
            var shots = chunk.GetSpan<Shot>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var shot = ref shots[i];
                shot.SinceTrail += deltaSeconds;
                if (shot.SinceTrail < TrailInterval || shot.IsSpent)
                    continue;

                shot.SinceTrail = 0f;
                if (velocities[i].Value.LengthSquared() > MinSpeed * MinSpeed || shot.Kind == ShotKind.Bomb)
                    effects.Trail(Commands, positions[i].Value, shot.Kind);
            }
        }
    }
}
