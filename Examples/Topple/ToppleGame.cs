using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Topple.Levels;
using Topple.Scenes;

namespace Topple;

/// <summary>
/// A catapult game built to show off <c>ConsoleEngine.Physics</c>: knock down towers of glass, wood and stone until every
/// crystal is broken. Square font cells, so shapes keep their proportions. Each level is a fresh
/// <see cref="LevelScene"/>; the <see cref="Campaign"/> carries the level number and score between them.
/// </summary>
public class ToppleGame() : GameBase(ScreenWidth, ScreenHeight, fontWidth: 10, fontHeight: 10)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int ScreenWidth = 120;
    public const int ScreenHeight = 60;

    //**********************************************************
    //** props
    //**********************************************************

    public Campaign Campaign { get; } = new(LevelBook.Levels.Length);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>(Re)starts the campaign's current level.</summary>
    public void LoadLevel() => Scenes.Set(new LevelScene(LevelBook.Levels[Campaign.LevelIndex]));

    //**********************************************************
    //** internals
    //**********************************************************

    protected override void OnInitialize() => LoadLevel();

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed)
            Stop();
    }

    protected override void OnRender() { }
}
