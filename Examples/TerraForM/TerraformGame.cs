using System;
using ConsoleEngine;
using TerraForM.Scenes;

namespace TerraForM;

public class TerraformGame : GameBase
{
    //**********************************************************
    //** props: 
    //**********************************************************

    public Camera Camera { get; private set; }
    public string Playername;
    public int Score;
    public int CurrentMap = 1;
    public bool GameOver;
    public bool IsDebugMode;
    public string[] Maps = {
        // "Assets/Maps/map0.txt",
        "Assets/Maps/map1.txt",
        "Assets/Maps/map2.txt",
        "Assets/Maps/map3.txt",
        "Assets/Maps/map4.txt"
    };

    //**********************************************************
    //** ctor:
    //**********************************************************

    public TerraformGame() : base(
        width: 72,
        height: 50,
        fontWidth: 15,
        fontHeight: 15)
    {
        Console.ClearColor = ConsoleColor.Black;
    }

    //**********************************************************
    //** public methods:
    //**********************************************************

    public void RotateMap()
    {
        CurrentMap++;
        if (CurrentMap >= Maps.Length) {
            CurrentMap = 0;
        }
        Scenes.Set(new GameScene($"map{CurrentMap}"));
    }
        
    protected override void OnInitialize()
    {
        Scenes.Set<InputNameScene>();
        Camera = new Camera(this);
    }
          
    //**********************************************************
    //** overrides:
    //**********************************************************

    protected override void OnUpdate()
    {
        Camera.Update();
    }

    protected override void OnRender() { }
}