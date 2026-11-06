using ConsoleEngine;
using ConsoleEngine.Abstractions.Rendering;
using Snake.Ecs.Scenes;

namespace Snake.Ecs;

/// <summary><c>Examples/Snake</c> on the ECS. Everything is set up in <see cref="SnakeScene"/>.</summary>
public class SnakeEcsGame() : GameBase(30, 30, new FontInfo
{
    FontWidth = 14,
    FontHeight = 14,
    FontFace = "Consolas"
})
{
    protected override void OnInitialize() => Scenes.Set(new SnakeScene());

    protected override void OnUpdate() { }

    protected override void OnRender() { }
}
