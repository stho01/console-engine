using ConsoleEngine.Infrastructure;

namespace MyAwesomeConsoleGame
{
    public class Wait : Command
    {
        public Wait(float durationInMilliseconds) : base(durationInMilliseconds)
        {
        }

        public override void OnUpdate(Rover rover)
        {
            // Just wait
        }
    }
}