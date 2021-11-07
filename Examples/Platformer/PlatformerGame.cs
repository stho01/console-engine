using System.Numerics;
using ConsoleEngine;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Native;
using Platformer.GameObjects;

namespace Platformer
{
    public class PlatformerGame : GameBase
    {
        private Player _player;
        private Camera _camera;
        private World _world;
        
        public PlatformerGame() 
            : base(new RenderConsole(new ConsoleHandler(60, 60,new FontInfo {
                FontWidth = 10,
                FontHeight = 10,
                FontFace = "Consolas"
            })), "Platformer game") {}

        public Camera Camera => _camera;
        public World World => _world;
        public bool IsDebugMode { get; set; } = true;

        protected override void OnInitialize()
        {
            ShowFps = true;

            _player = new Player(this) {
                Position = new Vector2(10, Console.Height - 10)
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

            if (IsDebugMode)
            {
                Console.Draw(0, 0, (_player.Position).ToString()); 
                Console.Draw(0, 1, (((int)(_player.Position.X/4), (int)(_player.Position.Y/4))).ToString()); // Player pos in tile map coordinates
                Console.Draw(0, 2, _player.Velocity.ToString());   
                Console.Draw(0, 3, _player.IsAirborne.ToString());   
            }
        }
    }
}