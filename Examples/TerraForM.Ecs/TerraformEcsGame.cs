using System;
using ConsoleEngine;
using TerraForM.Ecs.Scenes;

namespace TerraForM.Ecs;

/// <summary>
/// The TerraForM example rebuilt on the ECS. Name entry and the menu are plain scenes, as in
/// <c>Examples/TerraForM</c>. A level is a <see cref="GameScene"/>: map tiles, the rover and plant emitters are
/// entities, and the level rules run as systems.
/// </summary>
public class TerraformEcsGame : GameBase
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>
    /// Same as the original's <c>Maps.Length</c>, the only part of that array it uses. Maps rotate
    /// map1 → map2 → map3 → map0 → map1, so map4 is never played.
    /// </summary>
    private const int MapCount = 4;

    //**********************************************************
    //** ctor
    //**********************************************************

    public TerraformEcsGame() : base(width: 72, height: 50, fontWidth: 15, fontHeight: 15)
    {
        Console.ClearColor = ConsoleColor.Black;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public string PlayerName { get; set; }

    /// <summary>Running score. Never reset: not by a restart, game over or a new level.</summary>
    public int Score { get; set; }

    public int CurrentMap { get; private set; } = 1;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void RotateMap()
    {
        CurrentMap++;
        if (CurrentMap >= MapCount)
            CurrentMap = 0;

        Scenes.Set(new GameScene($"map{CurrentMap}"));
    }

    //**********************************************************
    //** overrides
    //**********************************************************

    protected override void OnInitialize() => Scenes.Set<InputNameScene>();

    protected override void OnUpdate() { }

    protected override void OnRender() { }
}
