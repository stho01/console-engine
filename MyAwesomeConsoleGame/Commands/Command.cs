namespace MyAwesomeConsoleGame
{
    public abstract class Command
    {
        public readonly float DurationInMilliseconds;

        public Command(float durationInMilliseconds)
        {
            DurationInMilliseconds = durationInMilliseconds;
        }

        public abstract void Update(Rover rover);

        public abstract bool IsDone();
    }
}