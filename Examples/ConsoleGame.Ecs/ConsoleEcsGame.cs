using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleGame.Ecs.Scenes;

namespace ConsoleGame.Ecs;

/// <summary>
/// <c>Examples/ConsoleGame</c> on the ECS: every cell is an entity with a <c>Position</c> and a <c>Glyph</c>, recoloured
/// by a system every 500 ms. <c>ESC</c> quits.
/// </summary>
public class ConsoleEcsGame() : GameBase(Width, Height, fontWidth: 8, fontHeight: 8)
{
    public const int Width = 64;
    public const int Height = 64;

    protected override void OnInitialize() => Scenes.Set(new ColorGridScene());

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Stop();
    }

    protected override void OnRender() { }
}
