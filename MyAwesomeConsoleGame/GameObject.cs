using System.Runtime.Serialization;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class GameObject
    {
        public MyAwesomeGame Game { get; }
        public Vector2 Position { get; set; }

        public GameObject(MyAwesomeGame game)
        {
            Game = game;
        }

        public Point GetScreenPos() {
            return Game.Camera.WorldToScreenPos(Position);
        }
    }
}