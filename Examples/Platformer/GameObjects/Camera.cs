using System;
using Microsoft.Xna.Framework;

namespace Platformer.GameObjects
{
    public class Camera
    {
        private readonly PlatformerGame _game;
        private GameObject _gameObject;

        public Camera(PlatformerGame game)
        {
            _game = game;
        }
        
        public Vector2 Position { get; set; }
        public int Padding { get; set; } = 2;
        
        public void Follow(GameObject gameObject) {
            _gameObject = gameObject;
        }

        public void Update()
        {
            var screenPos = (_gameObject.Position - Position);
            if (screenPos.X < 0) {
                Position += new Vector2((float)Math.Floor(screenPos.X), Position.Y);
            }
            
            if ((screenPos.X + _gameObject.CollisionBox.Width) >= (_game.Console.Width))
            {
                var displacement = Math.Ceiling((screenPos.X + _gameObject.CollisionBox.Width) - _game.Console.Width);
                Position += new Vector2((float)displacement, Position.Y);
            }
            
        }

        public Vector2 WorldPosToScreenPos(Vector2 worldPos) => (worldPos - Position);
    }
}