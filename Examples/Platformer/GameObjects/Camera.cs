using System;
using ConsoleEngine;
using Microsoft.Xna.Framework;

namespace Platformer.GameObjects
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


            //var (x, y) = WorldToScreenPos(_gameObject.Position);
            // if (x < 0) {
            //     Position += new Vector2((float)Math.Floor(x), 0);
            // } else if (x + (_gameObject.CollisionBox?.Width ?? 0) > _game.Console.Width) {
            //     Position += new Vector2((float)Math.Ceiling((x + (_gameObject.CollisionBox?.Width ?? 0))-_game.Console.Width-1), 0);
            // }
            //
            // if (y < 0) {
            //     Position += new Vector2(0, (float)Math.Floor(y));
            // } else  if (y + (_gameObject.CollisionBox?.Height ?? 0) > _game.Console.Height) {
            //     Position += new Vector2(0, (float)Math.Ceiling((y + (_gameObject.CollisionBox?.Height ?? 0))-_game.Console.Height-1));
            // }
        }
    }
}