using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Vikingferd.Puzzle;
using Vikingferd.Scenes;

namespace Vikingferd;

/// <summary>
/// A puzzle platformer: guide three vikings with different abilities through each level, and get all of them to the
/// exit. Starts on the <see cref="TitleScene"/> (level select); each level is one ECS scene, <see cref="LevelScene"/>.
/// Moving between scenes (restart, next level, back to the title) happens here, after the scene's update. 8 x 16 font
/// so the 4 x 2 cell tiles are square.
/// </summary>
public class VikingferdGame() : GameBase(Layout.ScreenWidth, Layout.ScreenHeight, fontWidth: 8, fontHeight: 16)
{
    protected override void OnInitialize() => Scenes.Set(new TitleScene(selectedLevel: 0));

    protected override void OnUpdate()
    {
        if (Scene is not LevelScene level)
            return;

        var input = Input.Instance;
        if (input.GetKey(Key.ESCAPE).Pressed)
            Scenes.Set(new TitleScene(level.LevelIndex));
        else if (input.GetKey(Key.R).Pressed)
            Scenes.Set(new LevelScene(level.LevelIndex));
        else if (level.Expedition.Outcome == Outcome.Won && IsConfirmPressed(input))
            Scenes.Set(level.LevelIndex + 1 < Levels.All.Length
                ? new LevelScene(level.LevelIndex + 1)
                : new TitleScene(selectedLevel: 0));
    }

    protected override void OnRender() { }

    //**********************************************************
    //** internals
    //**********************************************************

    private static bool IsConfirmPressed(Input input) =>
        input.GetKey(Key.ENTER).Pressed || input.GetKey(Key.E).Pressed || input.GetKey(Key.SPACE).Pressed;
}
