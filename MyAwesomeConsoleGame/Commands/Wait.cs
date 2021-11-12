using ConsoleEngine.Infrastructure;

namespace MyAwesomeConsoleGame
{
    public class Wait : Command
    {
        private double _elapsed = 0.0f;
        private bool _done = false;
        public Wait(float duration) : base(duration)
        {
        }

        public override void Update(Rover rover)
        {
            _elapsed += GameTime.Delta.TotalMilliseconds;
            if (_elapsed > DurationInMilliseconds)
            {
                _done = true;
            }
        }

        public override bool IsDone()
        {
            return _done;
        }
    }
}