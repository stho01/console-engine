namespace MyAwesomeConsoleGame
{
    public class Plant : Command
    {
        public Plant() : base(1.0f)
        {
            
        }

        protected override void OnUpdate(Rover rover)
        {
            rover.Plant();
        }

        public override char GetVisualRepresentation()
        {
            return 'P';
        }
    }
}