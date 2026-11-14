using ConsoleEngine.Ecs;
using Kartster.Components;

namespace Kartster.Race;

/// <summary>
/// Kartster's autopilot (<c>RaceSimulation.TakeOver</c>): once the player has finished, a bot drives their kart on.
/// Gives the finished <see cref="PlayerKart"/> a <see cref="Bot"/> at <see cref="Skill"/> on the centerline, so the
/// <c>BotDriverSystem</c> drives it from the next step and the <c>KartInputSystem</c> (which skips bots) lets go.
/// </summary>
[UpdateAfter(typeof(RaceSystem))]
public sealed class AutopilotSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's <c>BotProfile.Autopilot</c> is Merlin on a clean line: the top skill.</summary>
    public const float Skill = 1f;

    private Query _finished;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _finished = World.Query(Query.With<PlayerKart, RaceProgress>().Without<Bot>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _finished.GetChunks())
        {
            var entities = chunk.Entities;
            var progress = chunk.GetSpan<RaceProgress>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (progress[i].Finished)
                    Commands.Add(entities[i], new Bot { Skill = Skill, Sample = -1 });
            }
        }
    }
}
