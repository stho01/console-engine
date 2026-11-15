using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Scripted.Scenes;

namespace Scripted;

/// <summary>
/// Entities whose behaviour is a Lua script in <c>Assets/Scripts</c>. Everything is set up in
/// <see cref="ScriptedScene"/>. F5 reloads the scripts from the output folder while the game runs.
/// </summary>
public class ScriptedGame() : GameBase(80, 40, 12, 12)
{
    protected override void OnInitialize() => Scenes.Set(new ScriptedScene());

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Stop();
    }

    protected override void OnRender() { }
}
