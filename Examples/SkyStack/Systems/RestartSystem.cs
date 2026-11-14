using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using SkyStack.Climbing;

namespace SkyStack.Systems;

/// <summary>
/// SPACE or ENTER on the game-over box starts a new run. Waits <see cref="DelaySeconds"/> first, so a jump key still
/// held from the fall doesn't skip the box.
/// </summary>
public sealed class RestartSystem(RunStats run, Action restart) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float DelaySeconds = .75f;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        if (!run.IsOver || run.SecondsSinceOver < DelaySeconds)
            return;

        var input = Input.Instance;
        if (input.GetKey(Key.SPACE).Pressed || input.GetKey(Key.ENTER).Pressed)
            restart();
    }
}
