using Microsoft.Xna.Framework;

namespace Platformer.GameObjects;

public abstract class GameObject
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Acceleration { get; set; }
}