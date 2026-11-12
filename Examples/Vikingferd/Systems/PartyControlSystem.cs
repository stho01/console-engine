using ConsoleEngine.Ecs;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Systems;

/// <summary>
/// Switches heroes (TAB, 1-3) and hands this frame's input to the active hero; the others stand still. Runs every
/// frame because key presses are one-frame edges: jump and use are latched on the actor until the next fixed step
/// (<see cref="PuzzleSystem"/>) consumes them. Once the attempt is over, nobody gets input.
/// </summary>
public sealed class PartyControlSystem(Expedition expedition) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _actors;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _actors = World.Query(Query.With<Actor>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var input = expedition.Input;
        var playing = expedition.Outcome == Outcome.Playing;
        if (playing && input.NextHero)
            expedition.SelectNextHero();
        if (playing && input.SelectHero > 0)
            expedition.SelectHero(input.SelectHero - 1);

        foreach (var chunk in _actors.GetChunks())
        {
            var actors = chunk.GetSpan<Actor>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (Heroes.IsHero(actors[i].Kind))
                    Control(ref actors[i], playing && actors[i].Kind == expedition.ActiveHero, input);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Control(ref Actor hero, bool active, in PartyInput input)
    {
        hero.Run = active ? input.Horizontal : 0;
        hero.JumpHeld = active && input.JumpHeld;
        if (active && input.JumpPressed)
            hero.JumpRequested = true;
        if (active && input.UsePressed)
            hero.UseRequested = true;
    }
}
