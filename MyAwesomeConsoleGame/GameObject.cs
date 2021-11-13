using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public abstract class GameObject
    {
        public MyAwesomeGame Game { get; }
        public Vector2 Position { get; set; }
        public virtual Rectangle BoundingBox => new((int)Position.X, (int)Position.Y, World.TileSize, World.TileSize);
        
        public GameObject(MyAwesomeGame game)
        {
            Game = game;
        }

        public Vector2 GetScreenPos() 
        {
            return Game.Camera.WorldToScreenPos(Position);
        }
    }
}