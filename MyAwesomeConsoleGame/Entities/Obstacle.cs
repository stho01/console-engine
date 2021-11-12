namespace MyAwesomeConsoleGame
{
    public abstract class Obstacle : GameObject
    {
        public Obstacle(MyAwesomeGame game) : base(game)
        {
        }

        public abstract void Draw();
    }
}