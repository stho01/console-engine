using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Play;

namespace Doom.Systems;

/// <summary>
/// Things that don't act: decorations and pickups loop their idle frames (<see cref="ThingState.Static"/>), corpses
/// show their last frame (<see cref="ThingState.Dead"/>). Both stay on their floor when it moves.
/// </summary>
public sealed class StaticThingSystem(Level level) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

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
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var thing = ref things[i];
                if (thing.State is not (ThingState.Static or ThingState.Dead) || thing.Removed)
                    continue;

                if (thing.State == ThingState.Static)
                    Animator.Advance(ref animations[i], ThingInfo.ById(thing.Type));
                if (level.SectorMoved(bodies[i]))
                    level.SettleOnFloor(entities[i]);
            }
        }
    }
}
