using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Play;

namespace Doom.Systems;

/// <summary>
/// Plays death and explosion frames, and the frames of short effects (puffs, blood, fog). An exploding thing deals
/// its <see cref="Explosive.PendingDamage"/> on its third frame. At the end the thing is removed
/// (<see cref="ThingInfo.RemoveAfterDeath"/>) or stays as a corpse (<see cref="ThingState.Dead"/>).
/// </summary>
public sealed class DyingSystem(Level level) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The frame (from 0) an explosion hurts on.</summary>
    private const int SplashFrame = 2;

    private Query _things;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _things = World.Query(Query.With<Thing, Body, Animation>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _things.GetChunks())
        {
            var entities = chunk.Entities;
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            var animations = chunk.GetSpan<Animation>();
            var explosives = chunk.Has<Explosive>() ? chunk.GetSpan<Explosive>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var thing = ref things[i];
                if (thing.State != ThingState.Dying || thing.Removed)
                    continue;

                ref var animation = ref animations[i];
                var info = ThingInfo.ById(thing.Type);
                Animator.Advance(ref animation, info);
                if (!explosives.IsEmpty && explosives[i].PendingDamage > 0 && animation.FrameIndex >= SplashFrame)
                {
                    var damage = explosives[i].PendingDamage;
                    explosives[i].PendingDamage = 0;
                    level.RadiusDamage(entities[i], explosives[i].Source, damage);
                }

                if ((thing.Flags & ThingFlags.Missile) == 0 && level.SectorMoved(bodies[i]))
                    level.SettleOnFloor(entities[i]);
                if (!animation.Done)
                    continue;

                if (info.RemoveAfterDeath)
                    level.Remove(entities[i]);
                else
                    thing.State = ThingState.Dead;
            }
        }
    }
}
