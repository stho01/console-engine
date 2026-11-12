using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using PhysicsSandbox.Scenes;

namespace PhysicsSandbox;

/// <summary>
/// Demo of the engine's physics (<c>ConsoleEngine.Physics</c>): balls and boxes fall, bounce, slide and stack on
/// fixed ledges and a moving platform. Square font cells, so circles look round. Everything is set up in
/// <see cref="SandboxScene"/>.
/// </summary>
public class PhysicsSandboxGame() : GameBase(100, 60, fontWidth: 10, fontHeight: 10)
{
    protected override void OnInitialize() => Scenes.Set(new SandboxScene());

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Stop();
    }

    protected override void OnRender() { }
}
