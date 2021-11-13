using System;
using System.Data;
using System.Linq;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using MyAwesomeConsoleGame.Entities;
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
        public int AcceleratorsPlanted = 0;
        public int RemainingSequences = 0;

        public override Rectangle BoundingBox
        {
            get
            {
                var sprite = GetRoverSprite();
                return new Rectangle(Position.ToPoint(), sprite.Size);
            }
        }

        public Direction Direction { get; private set; }
  
        public static readonly Sprite RoverSpriteSouth = Sprite.FromStringArray(new[]
        {
            @"┌──┐",
            @"║##║",
            @" >< ",
            @"║##║",
            @"\__/"
        });

        public static readonly Sprite RoverSpriteNorth = Sprite.FromStringArray(new[]
        {
            @"/¯¯\",
            @"║##║",
            @" >< ",
            @"║##║",
            @"└──┘"
        });

        public static readonly Sprite RoverSpriteEast = Sprite.FromStringArray(new[]
        {
            @"┌═ ═\",
            @"│#v#│",
            @"│#∧#│",
            @"└═ ═/"
        });

        public static readonly Sprite RoverSpriteWest = Sprite.FromStringArray(new[]
        {
            @"/═ ═┐",
            @"│#v#│",
            @"│#∧#│",
            @"\═ ═┘"
        });

        public Rover(MyAwesomeGame game) : base(game)
        {
            MaxPower = game.World.MaxPower;
            RemainingPower = MaxPower;
            RemainingSequences = game.World.Sequences;
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

            if (Game.IsDebugMode)
            {
                Game.Console.Draw(0, 3, $"NPos : {Position}");
                Game.Console.Draw(0, 4, $"Bonus: {StandingOnBonusSpot}");
                Game.Console.Draw(0, 5, $"Plant: {StandingOnPlantingSpot}");    
            }
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

            if (Game.World.Intersects(this, out var with))
            {
                if (with.Any(x => x is Rock || x is Craves))
                    Position -= new Vector2(0, 1);
            }
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
                            shouldStopMotions = true;
                            DamageTaken += 10;
                            break;
                        case Craves:
                            shouldStopMotions = true;
                            this.RemainingPower = 0;
                            break;
                        case PlantSpot:
                            StandingOnPlantingSpot = true;
                            break;
                        case BonusPoint:
                            StandingOnBonusSpot = true;
                            break;
                        case FinishPoint:
                            Game.Score += (int)RemainingPower - (DamageTaken);
                            Game.Score += (AcceleratorsPlanted * 10000);
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

        public void Plant()
        {
            if (StandingOnPlantingSpot)
            {
                if (Game.World.Intersects(this, out var with))
                {
                    foreach (var tile in with)
                    {
                        if (tile is PlantSpot plantSpot && plantSpot.HasBeenPlanted == false)
                        {
                            AcceleratorsPlanted++;
                            Game.Score += 10000;
                            plantSpot.HasBeenPlanted = true;
                            Game.PlantEmitters.Add(new PlantEmitter(Game) {
                                Position = Position
                            });
                        }
                    }
                }
                
               
            }
        }
    }
}