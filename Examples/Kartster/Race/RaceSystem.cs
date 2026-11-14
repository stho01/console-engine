using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Kartster.Systems;

namespace Kartster.Race;

/// <summary>
/// Runs the <see cref="RaceSession"/> in the fixed step, after the kart has moved: advances its clock, reports
/// every kart with <c>Components.Kart</c>, <c>Position</c> and <see cref="RaceProgress"/>, ranks them, and copies
/// each kart's <see cref="Racer"/> into its <see cref="RaceProgress"/>. Every such kart must be registered with the
/// session by its <c>Kart.Index</c> (its grid slot) first, so the index also finds its name and mesh.
/// </summary>
/// <remarks>
/// It does not touch the kart physics: during the countdown (<see cref="RaceSession.CanDrive"/> false) whoever writes
/// <c>KartControls</c> (the input system, the bots) holds the kart still, as Kartster zeroes every driving input.
/// </remarks>
[UpdateAfter(typeof(KartGroundSystem))]
public sealed class RaceSystem(RaceSession session) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _karts;

    //**********************************************************
    //** props
    //**********************************************************

    public RaceSession Session => session;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _karts = World.Query(Query.With<Components.Kart, Position, RaceProgress>());

    protected override void OnUpdate(float deltaSeconds)
    {
        session.Advance(deltaSeconds);

        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Components.Kart>();
            var positions = chunk.GetSpan<Position>();
            var progress = chunk.GetSpan<RaceProgress>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var position = positions[i].Value;
                var last = progress[i].HasLastPosition ? progress[i].LastPosition : position;
                session.Update(karts[i].Index, new(last.X, last.Y), new(position.X, position.Y));
                progress[i].LastPosition = position;
                progress[i].HasLastPosition = true;
            }
        }

        session.UpdatePositions();

        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Components.Kart>();
            var progress = chunk.GetSpan<RaceProgress>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var racer = ref session.Get(karts[i].Index);
                ref var mirror = ref progress[i];
                mirror.Lap = racer.CurrentLap;
                mirror.CompletedLaps = racer.CompletedLaps;
                mirror.LapStarted = racer.LapStarted;
                mirror.NextCheckpointIndex = racer.NextCheckpointIndex;
                mirror.MissedCheckpoints = racer.MissedCheckpoints;
                mirror.Position = racer.Position;
                mirror.LastLapSeconds = racer.LastLapSeconds;
                mirror.BestLapSeconds = racer.BestLapSeconds;
                mirror.Finished = racer.HasFinished;
                mirror.FinishTimeSeconds = racer.FinishTimeSeconds;
                mirror.FinishPosition = racer.FinishPosition;
            }
        }
    }
}
