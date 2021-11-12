using System.Collections.Generic;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class Rover : GameObject
    {

        public static readonly Sprite Sprite = Sprite.FromStringArray(new[]
        {
            "###",
            "###",
            "###",
        });

        public Rover(MyAwesomeGame game) : base(game)
        {
        }
        

        public void Draw()
        {
            var screenPos = GetScreenPos();
            Game.Console.Draw(Position.X, Position.Y, Sprite);
        }

        public void DoCommands(IEnumerable<Command> commands)
        {
            
        }
    }
}