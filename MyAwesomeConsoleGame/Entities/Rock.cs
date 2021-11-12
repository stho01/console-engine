using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame
{
    public class Rock : Obstacle
    {
        private static readonly Sprite Sprite = Sprite.FromStringArray(new[]
        {
            "RRR",
            "RRR",
            "RRR",
        });

        public Rock(MyAwesomeGame game) : base(game)
        {
            
        }

        public override void Draw()
        {
            var screenPos = GetScreenPos();
            Game.Console.Draw(screenPos.X, screenPos.Y, Sprite);
        }
    }
}