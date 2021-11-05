using Microsoft.Xna.Framework;

namespace Platformer.GameObjects
{
    public class GameObject
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public Vector2 Acceleration { get; set; }
        public virtual Rectangle CollisionBox => new((int)Position.X, (int)Position.Y, 1, 1);
    }
}