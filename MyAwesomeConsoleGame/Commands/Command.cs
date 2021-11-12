using ConsoleEngine.Infrastructure;

namespace MyAwesomeConsoleGame
{
    public abstract class Command
    {
        protected double _elapsed = 0.0f;
        public readonly float DurationInMilliseconds;

        public Command(float durationInMilliseconds)
        {
            DurationInMilliseconds = durationInMilliseconds;
        }

        public abstract void OnUpdate(Rover rover);

        public void Update(Rover rover)
        {
            OnUpdate(rover);
            _elapsed += GameTime.Delta.TotalMilliseconds;
        }

        public bool IsDone() => _elapsed > DurationInMilliseconds;
    }
}