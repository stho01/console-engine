using System;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;

namespace Platformer.Player
{
    public class PlayerController
    {
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

        public void Update(PlayerModel player, Func<Vector2, bool> updateStep)
        {
            ApplyForce(player, -player.Velocity * DragCoefficient); // apply drag. 

            var previousPos = player.Position;
            var dtStep = GameTime.DeltaTimeSeconds / 100;
            for (var i = 0; i < 100; i++)
            {
                player.Velocity += player.Acceleration * dtStep;
                player.Position += player.Velocity;
                player.Acceleration = Vector2.Zero;
                
                if (!updateStep(previousPos)) {
                    break;
                }
            }
        }

        public bool IsPlayerColliding(PlayerModel player, Rectangle[] boundingBoxes)
        {
            for (var i = 0; i < boundingBoxes.Length; i++)
                if (player.BoundingBox.Intersects(boundingBoxes[i]))
                    return true;

            return false;
        }

        public void PlayerGrounded(PlayerModel player, Vector2 position)
        {
            player.Velocity = Vector2.Zero;
            player.Acceleration = Vector2.Zero;
            player.Position = position;
            player.IsAirborne = false;
        }
    }
}