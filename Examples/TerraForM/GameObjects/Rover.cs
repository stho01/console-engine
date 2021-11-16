using System;
using System.Linq;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using TerraForM.Commands;
using TerraForM.GameObjects.Tiles;
using TerraForM.Scenes;

namespace TerraForM.GameObjects
{
    public class Rover : GameObject
    {
        //**********************************************************
        //** events:
        //**********************************************************

        public event EventHandler OnFinishReached;
        public event EventHandler OnAthmosphereGeneratorsPlanted;
        
        //**********************************************************
        //** fields:
        //**********************************************************

        private const float Thrust = 0.8f;
        private const float Drag = 5f;
        public readonly float MaxPower;
        public double RemainingPower;
        public int DamageTaken = 0;
        public int AthmosphereGeneratorsPlanted = 0;
        public int RemainingSequences = 0;
        private readonly GameScene _scene;
        private static readonly Sprite RoverSpriteSouth = Sprite.FromStringArray(new[] {
            @"┌──┐",
            @"║##║",
            @" ║║ ",
            @" ║║ ",
            @"║##║",
            @"\__/"
        });
        private static readonly Sprite RoverSpriteNorth = Sprite.FromStringArray(new[] {
            @"/¯¯\",
            @"║##║",
            @" ║║ ",
            @" ║║ ",
            @"║##║",
            @"└──┘"
        });
        private static readonly Sprite RoverSpriteEast = Sprite.FromStringArray(new[] {
            @"┌═  ═\",
            @"│#══#│",
            @"│#══#│",
            @"└═  ═/"
        });
        private static readonly Sprite RoverSpriteWest = Sprite.FromStringArray(new[] {
            @"/═  ═┐",
            @"│#══#│",
            @"│#══#│",
            @"\═  ═┘"
        });
              
        //**********************************************************
        //** ctor:
        //**********************************************************

        public Rover(GameScene scene) : base(scene.Game)
        {
            _scene = scene;
            MaxPower = scene.World.MaxPower;
            RemainingPower = MaxPower;
            RemainingSequences = scene.World.Sequences;
        }
              
        //**********************************************************
        //** props:
        //**********************************************************

        public Vector2 Velocity { get; set; }
        public Vector2 Acceleration { get; set; }
        public bool StandingOnPlantingSpot { get; private set; }
        public bool StandingOnBonusSpot { get; private set; }
        public override Rectangle BoundingBox
        {
            get
            {
                var sprite = GetRoverSprite();
                return new Rectangle(Position.ToPoint(), sprite.Size);
            }
        }
        public Direction Direction { get; private set; }
              
        //**********************************************************
        //** public methods:
        //**********************************************************

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

        public bool PowerDepleted() => RemainingPower <= 0;

        public void MoveNorth()
        {
            ApplyForce(new Vector2(0, -1f) * Thrust);
            Direction = Direction.North;

            if (!_scene.World.Intersects(this, out var with)) 
                return;
            
            if (with.Any(x => x is Rock or Craves))
                Position -= new Vector2(0, 1);
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
            if (PowerDepleted())
            {
                return;
            }
            Acceleration += force;
        }
        
        public void Plant()
        {
            if (StandingOnPlantingSpot && _scene.World.Intersects(this, out var with))
            {
                foreach (var tile in with)
                {
                    if (tile is PlantSpot plantSpot && plantSpot.HasBeenPlanted == false)
                    {
                        AthmosphereGeneratorsPlanted++;
                        Game.Score += 10000;
                        plantSpot.HasBeenPlanted = true;
                        OnAthmosphereGeneratorsPlanted?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        private void HandleCollision(Vector2 prevPosition)
        {
            var shouldStopMotions = false;

            if (_scene.World.Intersects(this, out var with))
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
                            RemainingPower = 0;
                            break;
                        case PlantSpot:
                            StandingOnPlantingSpot = true;
                            break;
                        case BonusPoint bp:
                            HandleBonusPointCollision(bp);
                            break;
                        case FinishPoint:
                            OnFinishReached?.Invoke(this, EventArgs.Empty);
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

        private void HandleBonusPointCollision(BonusPoint bp)
        {
            StandingOnBonusSpot = true;
            
            if (!bp.HasBeenConsumed)
            {
                bp.HasBeenConsumed = true;
                RemainingPower += 1000;
                Game.Score += 10000;
            }
        }

        private Sprite GetRoverSprite()
        {
            return Direction switch
            {
                Direction.North => RoverSpriteNorth,
                Direction.South => RoverSpriteSouth,
                Direction.East => RoverSpriteEast,
                _ => RoverSpriteWest
            };
        }
    }
}