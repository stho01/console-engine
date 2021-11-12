using System.Collections.Generic;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class Rover : GameObject
    {
        public Vector2 Velocity { get; set; }
        public Vector2 Acceleration { get; set; }
        public const float Thrust = 0.8f;
        public const float Drag = 20f;
        
        
        public static readonly Sprite Sprite = Sprite.FromStringArray(new[]
        {
            "###",
            "###",
            "###",
        });

        public Rover(MyAwesomeGame game) : base(game) {
            
        }

        public void Update()
        {
            Velocity += -Drag * Velocity * (float)GameTime.Delta.TotalSeconds;
            Velocity += Acceleration * (float)GameTime.Delta.TotalSeconds;
            Position += Velocity;
            Acceleration = Vector2.Zero;
        }
        
        public void Draw()
        {
            var screenPos = GetScreenPos();
            Game.Console.Draw(
                screenPos.X, 
                screenPos.Y, 
                Sprite);
        }

        public void MoveNorth() => ApplyForce(new Vector2(0, -1f) * Thrust);
        public void MoveSouth() => ApplyForce(new Vector2(0, 1f) * Thrust);
        public void MoveWest() => ApplyForce(new Vector2(-1f, 0) * Thrust);
        public void MoveEast() => ApplyForce(new Vector2(1f, 0) * Thrust);
        
        public void ApplyForce(Vector2 force) => Acceleration += force;
    }
}