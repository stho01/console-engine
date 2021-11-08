using ConsoleEngine;
using Microsoft.Xna.Framework;

namespace Astroids.GameObjects
{
    public class Camera
    {
        private readonly GameBase _game;
        private GameObject _gameObject;

        public Camera(GameBase game) 
        {
            _game = game;
        }
        
        public Vector2 Position { get; set; }
        
        public void Follow(GameObject gameObject) 
        {
            _gameObject = gameObject;
        }

        public Vector2 WorldToScreenPos(Vector2 worldPos) => (worldPos - Position);
        public Vector2 ScreenToWorldPos(Vector2 worldPos) => (worldPos + Position);
        
        public void Update()
        {
            Position = _gameObject.Position - _game.Console.ScreenCenter;
        }
    }
}