using System.Numerics;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure.Inputs;
using Platformer.GameObjects;

namespace Platformer;

public class PlatformerGame : GameBase
{
    private Player _player;
    private Camera _camera;
    private World _world;

    public PlatformerGame()
        : base(70, 35, new FontInfo {
            FontWidth = 10,
            FontHeight = 10,
            FontFace = "Consolas"
        }) 
    {
        Name = "Platformer game";
    }
        
    public Camera Camera => _camera;
    public World World => _world;
    public bool IsDebugMode { get; set; }

    protected override void OnInitialize()
    {
        ShowFps = true;

        _player = new Player(this) {
            Position = new Vector2(10, Console.Height - 5)
        };
            
        _camera = new Camera(this);
        _camera.Follow(_player);

        _world = new World(this);
    }

    protected override void OnUpdate() 
    {
        if (Input.Instance.GetKey(Key.F1).Pressed) IsDebugMode = !IsDebugMode;
        if (Input.Instance.GetKey(Key.A).Held) _player.MoveLeft();
        if (Input.Instance.GetKey(Key.D).Held) _player.MoveRight();
        if (Input.Instance.GetKey(Key.SPACE).Pressed) _player.Jump();
            
        _player.Update();
        _camera.Update();
    }

    protected override void OnRender()
    {
        _player.Draw();
        _world.Draw();
    }
}