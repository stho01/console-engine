using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using SkyStack.Climbing;
using SkyStack.Components;

namespace SkyStack.Systems;

/// <summary>
/// Moves every <see cref="Climber"/> with <see cref="ClimbPhysics.Step"/> and reports landings to the
/// <see cref="RunStats"/>. Runs in the fixed step; stops when the run is over, so the climber freezes where it fell.
/// </summary>
public sealed class ClimberMotionSystem(Tower tower, RunStats run) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _climbers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _climbers = World.Query(Query.With<Climber, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (run.IsOver)
            return;

        foreach (var chunk in _climbers.GetChunks())
        {
            var climbers = chunk.GetSpan<Climber>();
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var landed = ClimbPhysics.Step(ref climbers[i], ref positions[i].Value, ref velocities[i].Value,
                    tower, deltaSeconds);
                if (landed != ClimbPhysics.NoLanding)
                    run.Land(landed);
            }
        }
    }
}
