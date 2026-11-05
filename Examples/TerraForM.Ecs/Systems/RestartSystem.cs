using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using TerraForM.Ecs.Scenes;

namespace TerraForM.Ecs.Systems;

/// <summary><c>R</c> reloads the level's map at any time. The score is kept.</summary>
public sealed class RestartSystem(TerraformEcsGame game, Level level) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        if (Input.Instance.GetKey(Key.R).Pressed)
            game.Scenes.Set(new GameScene(level.MapName));
    }
}
