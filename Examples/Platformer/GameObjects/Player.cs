using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace Platformer.GameObjects
{
    public class Player : GameObject
    {
        private readonly PlatformerGame _game;
        private static readonly Sprite _playerSprite = Sprite.FromStringArray(new[]{
            " ☻ ",
            "╭▓╮",
            "╯▓╰",
            " ╿ "
        });
        
        public Player(PlatformerGame game) {
            _game = game; 
        }
        
        public override Rectangle CollisionBox => new(
            (int)Position.X, 
            (int)Position.Y,
            _playerSprite.Width, 
            _playerSprite.Height); 
        
        public bool IsAirborne { get; set; } = true;
        public float MovementStrength { get; set; } = 0.1f;
        public float DragCoefficient { get; set; } = 0.90f;
        
        public void MoveLeft() => ApplyForce(new Vector2(-1, 0) * MovementStrength);
        public void MoveRight() => ApplyForce(new Vector2(1, 0) * MovementStrength);
        public void MoveUp() => ApplyForce(new Vector2(0, -1) * MovementStrength);
        public void MoveDown() => ApplyForce(new Vector2(0, 1) * MovementStrength);
        
        public void Jump()
        {
            if (IsAirborne) 
                return;
            
            Log.Debug("Player jumped");
            
            ApplyForce(new Vector2(0, -30));
            IsAirborne = true;
        }
        
        public void ApplyForce(Vector2 force) => Acceleration += force;

        public void Update()
        {
            ApplyForce(-Velocity * DragCoefficient); // apply drag. 

            Velocity += Acceleration * (float)GameTime.Delta.TotalSeconds;
            Position += Velocity;
            
            Acceleration = Vector2.Zero;
        }

        public void Draw()
        {
            var (x, y) = _game.Camera.WorldToScreenPos(Position);

            _game.Console.Draw(
                (int)x,
                (int)y,
                _playerSprite
            );
        }
    }
}