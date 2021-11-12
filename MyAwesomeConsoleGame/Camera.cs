using ConsoleEngine;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class Camera
    {
        private readonly GameBase _game;
        private GameObject _gameObject;

        public Camera(GameBase game) 
        {
            _game = game;
        }
        
        public Point Position { get; set; }
        
        public void Follow(GameObject gameObject) 
        {
            _gameObject = gameObject;
        }

        public Point WorldToScreenPos(Point worldPos) => worldPos - Position;
        public Point ScreenToWorldPos(Point worldPos) => (worldPos + Position);
        
        public void Update()
        {
            Position = _gameObject.Position - _game.Console.ScreenCenter.ToPoint();
        }
    }
}