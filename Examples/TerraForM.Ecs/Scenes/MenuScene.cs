using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
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

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad()
    {
        var name = Game.PlayerName;
        if (string.IsNullOrEmpty(name))
            return;

        // Written over the ### placeholder of the shared story sprite, without clearing it first.
        Sprites.Story.Draw(32, 22, name[..Math.Min(name.Length, MaxNameLength)], ConsoleColor.Red, ConsoleColor.Black);
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

    public override void OnRender() => Game.Console.Draw(3, 5, Sprites.Story);
}
