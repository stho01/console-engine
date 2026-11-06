using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;

namespace Platformer.Ecs.Systems;

/// <summary>F1 toggles <see cref="PlatformerEcsGame.IsDebugMode"/>. Has no query: it only reads input.</summary>
public sealed class DebugToggleSystem(PlatformerEcsGame game) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        if (Input.Instance.GetKey(Key.F1).Pressed)
            game.IsDebugMode = !game.IsDebugMode;
    }
}
