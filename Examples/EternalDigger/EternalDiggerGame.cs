using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using EternalDigger.Scenes;

namespace EternalDigger;

/// <summary>
/// Dig down tile by tile, carry ore back to the house, buy ladders to climb out of deeper shafts. The whole game is
/// one ECS scene, <see cref="MineScene"/>. 8 x 16 font so the 4 x 2 cell tiles are square on Windows too.
/// </summary>
public class EternalDiggerGame(int seed)
    : GameBase(Layout.ScreenWidth, Layout.ScreenHeight, fontWidth: 8, fontHeight: 16)
{
    protected override void OnInitialize() => Scenes.Set(new MineScene(seed));

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Stop();
    }

    protected override void OnRender() { }
}
