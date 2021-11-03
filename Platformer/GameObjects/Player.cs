using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace Platformer.GameObjects
{
    public class Player
    {
        private readonly PlatformerGame _game;
        private static readonly Sprite _playerSprite = Sprite.FromStringArray(new[]{
            " 0 ",
            "╔▓╗",
            "╝▓╚",
            "╝ ╚ "
        });
        
        public Player(PlatformerGame game) {
            _game = game;
        }
        
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public Vector2 Acceleration { get; set; }
        public Rectangle BoundingBox => new((int)Position.X, (int)Position.Y, 3, 4);
        public bool IsAirborne { get; set; } = true;
        public float MovementStrength { get; set; } = 0.1f;
        public float DragCoefficient { get; set; } = 0.90f;
        
        
        public void MoveLeft() => ApplyForce(new Vector2(-1, 0) * MovementStrength);
        public void MoveRight() => ApplyForce(new Vector2(1, 0) * MovementStrength);
        public void Jump()
        {
            if (IsAirborne) 
                return;
            
            ApplyForce(new Vector2(0, -30));
            IsAirborne = true;
        }
        
        public void ApplyForce(Vector2 force)
        {
            Acceleration += force;
        }

        public void Update()
        {
            ApplyForce(-Velocity * DragCoefficient); // apply drag. 

            Velocity += Acceleration * (float)GameTime.DeltaTimeSeconds;
            Position += Velocity;
            
            Acceleration = Vector2.Zero;
        }

        public void Draw()
        {
            var (x, y) = (Position - _game.Camera.Position);

            _game.Console.Draw(
                (int)x,
                (int)y,
                _playerSprite
            );
        }
    }
}