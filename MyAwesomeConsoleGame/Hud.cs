using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Rendering;

namespace MyAwesomeConsoleGame
{
    public class Hud
    {
        private readonly MyAwesomeGame _game;
        private const int Top = 35;
        private readonly int _height;

        public Hud(MyAwesomeGame game)
        {
            _game = game;
            _height = _game.Console.Height - Top;
        }

        public void Draw()
        {
            for (var x = 0; x < _game.Console.Width; x++)
            for (var y = Top; y < _game.Console.Height; y++)
                _game.Console.Draw(x, y, ' ');
            
            _game.Console.DrawLine(0, Top, _game.Console.Width, Top, '▓');
        }

        public IEnumerable<Command> GetCommands()
        {
            // hardkode noe som funker
            
            return Enumerable.Empty<Command>();
        }
    }
}