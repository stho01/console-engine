using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using TerraForM.Ecs.Commands;
using TerraForM.Ecs.Components;
using TerraForM.Ecs.Scenes;

namespace TerraForM.Ecs.Systems;

/// <summary>
/// Runs first: updates <see cref="Level.GameOver"/>, queues commands from the arrow keys and <c>P</c>, opens the menu
/// on <c>ESC</c> and commits the queued commands on <c>SPACE</c> if none are running.
/// </summary>
public sealed class LevelInputSystem(TerraformEcsGame game, Level level) : EntitySystem
{
    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        ref var rover = ref World.Get<Rover>(level.Rover);
        level.GameOver = rover.PowerDepleted || rover.RemainingSequences < 0;

        var input = Input.Instance;
        var queue = level.CommandQueue;
        if (input.GetKey(Key.LEFT).Pressed) queue.Add(RoverCommand.Move(Direction.West));
        if (input.GetKey(Key.RIGHT).Pressed) queue.Add(RoverCommand.Move(Direction.East));
        if (input.GetKey(Key.UP).Pressed) queue.Add(RoverCommand.Move(Direction.North));
        if (input.GetKey(Key.DOWN).Pressed) queue.Add(RoverCommand.Move(Direction.South));
        if (input.GetKey(Key.P).Pressed) queue.Add(RoverCommand.Plant());

        if (input.GetKey(Key.ESCAPE).Held)
            game.Scenes.Push<MenuScene>();

        if (input.GetKey(Key.SPACE).Pressed && !queue.IsRunning && queue.Commit())
            rover.RemainingSequences--;
    }
}
