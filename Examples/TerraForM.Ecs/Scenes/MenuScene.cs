using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Infrastructure.Scenery;
using TerraForM.Assets;

namespace TerraForM.Ecs.Scenes;

/// <summary>
/// The story screen. <c>H</c> starts the current map, or returns to the paused level when the menu was opened with
/// <c>ESC</c>. A plain scene, as in the original.
/// </summary>
public class MenuScene : Scene<TerraformEcsGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int MaxNameLength = 30;

    private Sprite _story;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad()
    {
        // A copy, because the name is drawn into it and the loaded sprite is shared.
        _story = Assets.Load<Sprite>(SpritePaths.Story).Clone();

        var name = Game.PlayerName;
        if (string.IsNullOrEmpty(name))
            return;

        // Written over the ### placeholder.
        _story.Draw(32, 22, name[..Math.Min(name.Length, MaxNameLength)], ConsoleColor.Red, ConsoleColor.Black);
    }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        if (!Input.Instance.GetKey(Key.H).Pressed)
            return;

        if (Game.Scenes.Count == 1)
            Game.Scenes.Set(new GameScene($"map{Game.CurrentMap}"));
        else
            Game.Scenes.Pop();
    }

    public override void OnRender() => Game.Console.Draw(3, 5, _story);
}
