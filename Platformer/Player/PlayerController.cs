using System;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;

namespace Platformer.Player
{
    public class PlayerController
    {
        private readonly PlatformerGame _game;

        public PlayerController(PlatformerGame game)
        {
            _game = game;
        }
        
        public float MovementStrength { get; set; } = 10f;
        public float DragCoefficient { get; set; } = 0.95f;

        public void MoveLeft(PlayerModel player) => ApplyForce(player, new Vector2(-1, 0) * MovementStrength);
        public void MoveRight(PlayerModel player) => ApplyForce(player, new Vector2(1, 0) * MovementStrength);
        public void Jump(PlayerModel player)
        {
            if (player.IsAirborne) 
                return;
            
            ApplyForce(player, new Vector2(0, -30));
            player.IsAirborne = true;
        }

        public void ApplyForce(PlayerModel player, Vector2 force)
        {
            player.Acceleration += force;
        }

        public void Update(PlayerModel player)
        {
            if (player.IsAirborne)
                ApplyForce(player, PlatformerGame.Gravity);
            
            ApplyForce(player, -player.Velocity * DragCoefficient); // apply drag. 

            var previousPos = player.Position;
            const int steps = 25;
            var stepTime = GameTime.DeltaTimeSeconds / steps;
            
            for (var i = 0; i < steps; i++)
            {
                player.Velocity += player.Acceleration * (float)stepTime;
                player.Position += player.Velocity;
                
                if (_game.Console.GetCharAt(player.BoundingBox.Top, player.BoundingBox.Left) != ' '
                || _game.Console.GetCharAt(player.BoundingBox.Top, player.BoundingBox.Right) != ' '
                || _game.Console.GetCharAt(player.BoundingBox.Bottom, player.BoundingBox.Left) != ' '
                || _game.Console.GetCharAt(player.BoundingBox.Bottom, player.BoundingBox.Right) != ' ')
                {
                    player.Position = previousPos;
                    player.Velocity = Vector2.Zero;
                    player.Acceleration = Vector2.Zero;
                    player.IsAirborne = false;
                    break;
                }
            }
            
            if (player.Velocity.LengthSquared() <= 1)
                player.Velocity = Vector2.Zero;
            
            player.Acceleration = Vector2.Zero;
        }
    }
}