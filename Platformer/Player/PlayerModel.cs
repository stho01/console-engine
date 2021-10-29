using System;
using Microsoft.Xna.Framework;

namespace Platformer.Player
{
    public class PlayerModel
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public Vector2 Acceleration { get; set; }

        public Rectangle BoundingBox => new((int)Position.X, (int)Position.Y, 3, 4);
        public bool IsAirborne { get; set; } = true;
    }
}