using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using SkyStack.Climbing;
using SkyStack.Components;

namespace SkyStack.Systems;

/// <summary>
/// ← → / A D run, SPACE / ↑ / W held jumps. Runs every frame and only latches the keys into the
/// <see cref="Climber"/>; <see cref="ClimberMotionSystem"/> acts on them in the fixed step. After the run is over the
/// climber gets no input.
/// </summary>
public sealed class ClimberInputSystem(RunStats run) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _climbers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _climbers = World.Query(Query.With<Climber>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var input = Input.Instance;
        var direction = 0f;
        var jump = false;
        if (!run.IsOver)
        {
            if (input.GetKey(Key.LEFT).Held || input.GetKey(Key.A).Held) direction -= 1f;
            if (input.GetKey(Key.RIGHT).Held || input.GetKey(Key.D).Held) direction += 1f;
            jump = input.GetKey(Key.SPACE).Held || input.GetKey(Key.UP).Held || input.GetKey(Key.W).Held;
        }

        foreach (var chunk in _climbers.GetChunks())
        {
            var climbers = chunk.GetSpan<Climber>();
            for (var i = 0; i < chunk.Count; i++)
            {
                climbers[i].RunDirection = direction;
                climbers[i].JumpHeld = jump;
            }
        }
    }
}
