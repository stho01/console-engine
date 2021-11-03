using System.Numerics;
using ConsoleEngine;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Windows;
using Platformer.GameObjects;

namespace Platformer
{
    public class PlatformerGame : GameBase
    {
        private Player _player;
        public static readonly Vector2 Gravity = new(0, 0.01f);
        private Camera _camera;
        private UnendingGround _ground;
        
        public PlatformerGame() 
            : base(new RenderConsole(new ConsoleHandler(60, 60,new FontInfo {
                FontWidth = 10,
                FontHeight = 10,
                FontFace = "Consolas"
            })), "Platformer game") {}

        public Camera Camera => _camera;

        protected override void OnInitialize()
        {
            ShowFps = true;

            _player = new Player(this) {
                Position = new Vector2(10, Console.Height - 10)
            };
            
            _camera = new Camera(this);
            _camera.Follow(_player);

            _ground = new UnendingGround(this);
            _ground.Init();
        }

        protected override void OnUpdate() 
        {
            if (Input.Instance.GetKey(Key.A).Held) _player.MoveLeft();
            if (Input.Instance.GetKey(Key.D).Held) _player.MoveRight();
            if (Input.Instance.GetKey(Key.SPACE).Pressed) _player.Jump();

            _player.Update();
            _camera.Update();
        }

        protected override void OnRender()
        {
            _player.Draw();
            _ground.Draw();
        }
    }
}