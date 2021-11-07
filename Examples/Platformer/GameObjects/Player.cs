using System;
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
            "▓▓▓",
            "▓▓▓",
            "▓▓▓"
        });
   
        public Player(PlatformerGame game) {
            _game = game; 
        }
        
        public override Rectangle? CollisionBox => new(
            (int)Position.X, 
            (int)Position.Y,
            _playerSprite.Width, 
            _playerSprite.Height); 
        
        public bool IsAirborne { get; set; } = false;

        public float MovementStrength => IsAirborne ? 0.1f : .4f;
        
        public void MoveLeft() => ApplyForce(new Vector2(-1, 0) * MovementStrength);
        public void MoveRight() => ApplyForce(new Vector2(1, 0) * MovementStrength);
        
        public void Jump()
        {
            if (IsAirborne) 
                return;
            
            ApplyForce(new Vector2(0, -80));
            IsAirborne = true;
        }
        
        public void ApplyForce(Vector2 force) => Acceleration += force;
        public void ApplyForce(float x, float y) => Acceleration += new Vector2(x, y);

        public void Update()
        {
            ApplyForce(new Vector2(0f, .5f));
            ApplyDrag();
            
            Velocity += Acceleration;
            var newX = Position.X + Velocity.X * (float)GameTime.Delta.TotalSeconds;
            var newY = Position.Y + Velocity.Y * (float)GameTime.Delta.TotalSeconds;

            Position = ResolveCollision(newX, newY);
            
            if (Velocity.LengthSquared() < 0.1)
                Velocity = Vector2.Zero;
            Acceleration = Vector2.Zero;
        }

        private void ApplyDrag()
        {
            if (Velocity.X == 0)
                return;

            var c = IsAirborne ? .5 : 5;
            var dragX = -c * Velocity.X * GameTime.Delta.TotalSeconds;
            
            ApplyForce((float)dragX, 0f);
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

        private Vector2 ResolveCollision(float newX, float newY)
        {
            const float offset = .2f;
            var world = _game.World;
            
            var left = newX;
            var right = newX + _playerSprite.Width;
            var top = newY;
            var bottom = newY + _playerSprite.Height;
            
            if (Velocity.X >= 0 && (world.GetTile(right, top + offset) != '.' || world.GetTile(right, bottom - offset) != '.')) // right
            {
                newX = (int)newX;
                Velocity = new Vector2(0, Velocity.Y);
            } 
            else if (world.GetTile(left, top + offset) != '.' || world.GetTile(left, bottom - offset) != '.') // left
            {
                newX = (int)(newX + .5f);
                Velocity = new Vector2(0, Velocity.Y);
            }

            if (Velocity.Y >= 0 && world.GetTile(left + offset, bottom) != '.' || world.GetTile(right - offset, bottom) != '.') // down
            {
                newY = (int)newY;
                Velocity = new Vector2(Velocity.X, 0);
                IsAirborne = false;
            }
            else if (world.GetTile(left + offset, top) != '.' || world.GetTile(right - offset, top) != '.') // up
            {
                newY = (int)(newY + .5f);
                Velocity = new Vector2(Velocity.X, 0);
            }

            return new Vector2(newX, newY);
        }
    }
}