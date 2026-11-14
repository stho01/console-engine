using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Components;
using Kartster.Race;

namespace Kartster.Modes;

/// <summary>
/// Feeds the <see cref="TimeTrial"/> the <see cref="PlayerKart"/>'s pose each fixed step, after the
/// <see cref="RaceSystem"/> has counted the step's laps.
/// </summary>
[UpdateAfter(typeof(RaceSystem))]
public sealed class TimeTrialSystem(TimeTrial trial, RaceSession race) : EntitySystem
{
    private Query _player;

    protected override void OnCreate() => _player = World.Query(Query.With<PlayerKart, Kart, Position>().With<Rotation>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _player.GetChunks())
        {
            if (chunk.Count == 0)
                continue;

            var kart = chunk.GetSpan<Kart>()[0];
            var position = chunk.GetSpan<Position>()[0].Value;
            var radians = chunk.GetSpan<Rotation>()[0].Radians;
            trial.Step(race.Get(kart.Index), race.Phase, new GhostPose(position.X, position.Y, kart.Height, radians));
            return;
        }
    }
}
