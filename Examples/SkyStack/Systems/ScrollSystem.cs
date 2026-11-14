using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using Microsoft.Xna.Framework;
using SkyStack.Climbing;
using SkyStack.Components;

namespace SkyStack.Systems;

/// <summary>
/// Ticks the combo timer, rises the view with the <see cref="Scroller"/> and ends the run when the climber dropped
/// below the screen (the best score is recorded on the game then). Runs in the fixed step, after
/// <see cref="ClimberMotionSystem"/>. The camera is kept on whole rows, so tower and climber round the same way.
/// </summary>
public sealed class ScrollSystem(Scroller scroller, RunStats run, Camera camera, SkyStackGame game) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _climbers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _climbers = World.Query(Query.With<Climber, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        run.Tick(deltaSeconds);
        if (run.IsOver)
            return;

        foreach (var chunk in _climbers.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var row = positions[i].Value.Y;
                scroller.Update(deltaSeconds, row, run.HighestFloor);
                if (!scroller.HasLost(row))
                    continue;

                run.Finish();
                game.RecordRun(run.Score, run.HighestFloor);
            }
        }

        camera.Position = new Vector2(0f, MathF.Round(scroller.Top));
    }
}
