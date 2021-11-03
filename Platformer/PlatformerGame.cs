using ConsoleEngine;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;
using Platformer.Maps;
using Platformer.Player;

namespace Platformer
{
    public class PlatformerGame : GameBase
    {
        private PlayerModel _playerModel;
        private PlayerRenderer _playerRenderer;
        private PlayerController _playerController;
        private IMap _map;
        private MapRenderer _mapRenderer;
        public static readonly Vector2 Gravity = new(0, 0.01f);

        public PlatformerGame() 
            : base(new RenderConsole(60, 60) {
                FontWidth = 10,
                FontHeight = 10,
            }, "Platformer game") {}

        protected override void OnInitialize()
        {
            ShowFps = true;
            
            _playerModel = new PlayerModel {
                Position = new Vector2(10, 10)
            };
            _playerRenderer = new PlayerRenderer(Console);
            _playerController = new PlayerController(this);

            _map = new Level1();
            _mapRenderer = new MapRenderer(Console);
        }

        protected override void OnUpdate()
        {
            if (Input.Instance.GetKey(Key.A).Pressed) _playerController.MoveLeft(_playerModel);
            if (Input.Instance.GetKey(Key.D).Pressed) _playerController.MoveRight(_playerModel);
            if (Input.Instance.GetKey(Key.SPACE).Pressed) _playerController.Jump(_playerModel);

            _playerController.Update(_playerModel);
        }

        protected override void OnRender()
        {
            _mapRenderer.Draw(_map);
            _playerRenderer.Draw(_playerModel);
        }
    }
}