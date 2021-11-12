using ConsoleEngine.Infrastructure;

namespace MyAwesomeConsoleGame
{
    public class Wait : Command
    {
        public Wait(float durationInMilliseconds) : base(durationInMilliseconds)
        {
        }

        protected override void OnUpdate(Rover rover)
        {
            // Just wait
        }
    }
}