namespace MyAwesomeConsoleGame
{
    public class Move : Command
    {
        public readonly Direction Direction;

        public Move(Direction direction, float duration ) : base(duration)
        {
            Direction = direction;
        }

        public override void Update(Rover rover)
        {
            throw new System.NotImplementedException();
        }

        public override bool IsDone()
        {
            throw new System.NotImplementedException();
        }
    }
}