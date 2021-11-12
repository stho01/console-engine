namespace MyAwesomeConsoleGame
{
    public class Plant : Command
    {
        public Plant(float duration) : base(duration)
        {
            
        }

        protected override void OnUpdate(Rover rover)
        {
            throw new System.NotImplementedException();
        }
    }
}