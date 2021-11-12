using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class GameObject
    {
        public MyAwesomeGame Game { get; }
        public Point Position { get; set; }


        public GameObject(MyAwesomeGame game)
        {
            Game = game;
        }

        public Point GetScreenPos()
        {
            return Game.Camera.WorldToScreenPos(Position);
        }
    }
}