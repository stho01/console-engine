using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using EternalDigger.Saving;
using EternalDigger.Scenes;

namespace EternalDigger;

/// <summary>
/// Dig down tile by tile, carry ore back to the house, buy ladders to climb out of deeper shafts. Starts on the
/// <see cref="TitleScene"/> (CONTINUE / NEW MINE); the game itself is one ECS scene, <see cref="MineScene"/>.
/// <paramref name="seed"/> is used for a new mine. <c>ESC</c> saves (in the mine) and quits. 8 x 16 font so the
/// 4 x 2 cell tiles are square on Windows too.
/// </summary>
public class EternalDiggerGame(int seed)
    : GameBase(Layout.ScreenWidth, Layout.ScreenHeight, fontWidth: 8, fontHeight: 16)
{
    protected override void OnInitialize() => Scenes.Set(new TitleScene(seed, SaveFile.DefaultPath));

    protected override void OnUpdate()
    {
        if (!Input.Instance.GetKey(Key.ESCAPE).Pressed)
            return;

        if (Scene is MineScene mine)
            mine.Save();

        Stop();
    }

    protected override void OnRender() { }
}
