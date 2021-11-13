using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using MyAwesomeConsoleGame.Entities.Tiles;

namespace MyAwesomeConsoleGame
{
    public class Rover : GameObject
    {
        public Vector2 Velocity { get; set; }
        public Vector2 Acceleration { get; set; }
        public const float Thrust = 0.8f;
        public const float Drag = 5f;
        public bool StandingOnPlantingSpot { get; private set; }
        public bool StandingOnBonusSpot { get; private set; }
        public float MaxPower;
        public double RemainingPower;
        public int DamageTaken = 0;

        public Direction Direction { get; private set; }

        public static readonly Sprite RoverSpriteSouth = Sprite.FromStringArray(new[]
        {
            "┌───┐",
            "║###║",
            "│###│",
            "║###║",
           "\\___/"
        });

        public static readonly Sprite RoverSpriteNorth = Sprite.FromStringArray(new[]
        {
            "/¯¯¯\\",
            "║###║",
            "│###│",
            "║###║",
            "└───┘"
        });

        public static readonly Sprite RoverSpriteEast = Sprite.FromStringArray(new[]
        {
            "┌═─═\\",
            "│###│",
            "│###│",
            "│###│",
            "└═─═/"
        });

        public static readonly Sprite RoverSpriteWest = Sprite.FromStringArray(new[]
        {
            "/═─═┐",
            "│###│",
            "├###│",
            "│###│",
           "\\═─═┘"
        });

        public Rover(MyAwesomeGame game) : base(game)
        {
            MaxPower = game.World.MaxPower;
            RemainingPower = MaxPower;
        }

        public void Update()
        {
            StandingOnBonusSpot = false;
            StandingOnPlantingSpot = false;

            var prevPosition = Position;

            Velocity += -Drag * Velocity * (float)GameTime.Delta.TotalSeconds;
            Velocity += Acceleration * (float)GameTime.Delta.TotalSeconds;
            Position += Velocity;

            HandleCollision(prevPosition);

            Acceleration = Vector2.Zero;

            Game.Console.Draw(0, 3, $"NPos : {Position}");
            Game.Console.Draw(0, 4, $"Bonus: {StandingOnBonusSpot}");
            Game.Console.Draw(0, 5, $"Plant: {StandingOnPlantingSpot}");
        }

        public void Draw()
        {
            var screenPos = GetScreenPos();
            Game.Console.Draw(
                (int)screenPos.X - 1,
                (int)screenPos.Y - 1,
                GetRoverSprite());
        }

        public void MoveNorth()
        {
            ApplyForce(new Vector2(0, -1f) * Thrust);
            Direction = Direction.North;
        }

        public void MoveSouth()
        {
            ApplyForce(new Vector2(0, 1f) * Thrust);
            Direction = Direction.South;
        }

        public void MoveWest()
        {
            ApplyForce(new Vector2(-1f, 0) * Thrust);
            Direction = Direction.West;
        }

        public void MoveEast()
        {
            ApplyForce(new Vector2(1f, 0) * Thrust);
            Direction = Direction.East;
        }


        public void ApplyForce(Vector2 force)
        {
            RemainingPower -= force.LengthSquared();
            if (RemainingPower <= 0)
            {
                return;
            }
            Acceleration += force;
        }

        private void HandleCollision(Vector2 prevPosition)
        {
            var shouldStopMotions = false;

            if (Game.World.Intersects(this, out var with))
            {
                foreach (var tile in with)
                {
                    switch (tile)
                    {
                        case Rock:
                        case Craves:
                            shouldStopMotions = true;
                            break;
                        case PlantSpot:
                            StandingOnPlantingSpot = true;
                            break;
                        case BonusPoint:
                            StandingOnBonusSpot = true;
                            break;
                        case FinishPoint:
                            Game.RotateMap();
                            break;
                    }
                }
            }

            if (shouldStopMotions)
            {
                Position = prevPosition;
                Velocity = Vector2.Zero;
            }
        }

        private Sprite GetRoverSprite()
        {
            switch ((Direction)
)
            {
                case Direction.North:
                    return RoverSpriteNorth;
                case Direction.South:
                    return RoverSpriteSouth;
                case Direction.East:
                    return RoverSpriteEast;
                default:
                    return RoverSpriteWest;
            }
        }
    }
}