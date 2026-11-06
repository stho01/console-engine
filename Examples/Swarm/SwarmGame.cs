using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Swarm.Scenes;

namespace Swarm;

/// <summary>
/// ECS stress test: many particles chasing a moving attractor. Watch the FPS in the logger window while adding
/// particles with <c>SPACE</c>. Everything is set up in <see cref="SwarmScene"/>.
/// </summary>
public class SwarmGame(int particleCount) : GameBase(160, 100, fontWidth: 6, fontHeight: 6)
{
    protected override void OnInitialize() => Scenes.Set(new SwarmScene(particleCount));

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Stop();
    }

    protected override void OnRender() { }
}
