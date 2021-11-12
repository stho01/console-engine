using System;
using ConsoleEngine.Infrastructure;

namespace MyAwesomeConsoleGame
{
    public class Move : Command
    {
        public readonly Direction Direction;

        public Move(Direction direction, float duration = 100 ) : base(duration)
        {
            Direction = direction;
        }

        protected override void OnUpdate(Rover rover)
        {
           
            switch (Direction)
            {
                case Direction.East:
                    rover.MoveEast();
                    break;
                case Direction.West:
                    rover.MoveWest();
                    break;
                case Direction.North:
                    rover.MoveNorth();
                    break;
                case Direction.South:
                    rover.MoveSouth();
                    break;
                default:
                    throw new Exception("no go");
            }
        }

        public override char GetVisualRepresentation()
        {
            switch (Direction)
            {
                case Direction.East:
                    return '→';
                case Direction.West:
                    return '←';
                case Direction.North:
                    return '↑';
                case Direction.South:
                    return '↓';
                default:
                    throw new Exception("no go");
            }
        }
    }
}