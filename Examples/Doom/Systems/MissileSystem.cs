using System;
using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Play;

namespace Doom.Systems;

/// <summary>
/// Flies projectiles one tic: in steps short enough not to skip thin walls, exploding on a wall, the floor or
/// ceiling, or a thing (which takes the impact damage). Flying into the sky makes them vanish.
/// </summary>
public sealed class MissileSystem(Level level) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Momentum is split into steps no longer than this.</summary>
    private const float MaxStep = 15;

    private Query _missiles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _missiles = World.Query(Query.With<Thing, Body, Animation>().With<Momentum, Missile>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _missiles.GetChunks())
        {
            var entities = chunk.Entities;
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            var animations = chunk.GetSpan<Animation>();
            var momenta = chunk.GetSpan<Momentum>();
            var missiles = chunk.GetSpan<Missile>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (things[i].State != ThingState.Flying || things[i].Removed)
                    continue;

                Animator.Advance(ref animations[i], ThingInfo.ById(things[i].Type));
                Fly(entities[i], ref things[i], ref bodies[i], ref momenta[i], missiles[i].Owner);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Fly(Entity missile, ref Thing thing, ref Body body, ref Momentum momentum, Entity owner)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(momentum.X), MathF.Abs(momentum.Y)) / MaxStep));
        var stepX = momentum.X / steps;
        var stepY = momentum.Y / steps;
        var stepZ = momentum.Z / steps;
        for (var i = 0; i < steps; i++)
        {
            body.Z += stepZ;
            if (!level.TryMove(missile, body.X + stepX, body.Y + stepY, out var check))
            {
                if (check.BlockingThing != Entity.Null)
                {
                    var info = ThingInfo.ById(thing.Type);
                    level.Damage(check.BlockingThing, missile, owner, level.RandomRange(info.DamageMin, info.DamageMax));
                }
                else if (check.BlockingLine is { Back: not null } line && level.IsSkyHole(line, body))
                {
                    level.Remove(missile);
                    return;
                }

                level.Explode(missile);
                return;
            }

            if (body.Z > body.FloorZ && body.Z + body.Height < body.CeilingZ)
                continue;

            if (body.Z + body.Height >= body.CeilingZ
                && level.Map.Sectors[body.Sector].CeilingFlat == level.Textures.SkyFlat)
            {
                level.Remove(missile);
                return;
            }

            level.Explode(missile);
            return;
        }
    }
}
