using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;

namespace Starguard.Systems;

/// <summary>
/// The only system that reads the keyboard. LEFT/A and RIGHT/D move, SPACE fires (hold for auto-fire), ENTER restarts
/// after game over. Runs first.
/// </summary>
public sealed class ControlsSystem(Controls controls) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        var input = Input.Instance;
        var move = 0;
        if (input.GetKey(Key.LEFT).Held || input.GetKey(Key.A).Held) move--;
        if (input.GetKey(Key.RIGHT).Held || input.GetKey(Key.D).Held) move++;

        controls.Move = move;
        controls.Fire = input.GetKey(Key.SPACE).Held;
        controls.Restart = input.GetKey(Key.ENTER).Pressed;
    }
}
