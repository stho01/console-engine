using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;

namespace MyAwesomeConsoleGame
{
    public class Hud
    {
        
        private readonly MyAwesomeGame _game;
        private const int Top = 35;
        private const int WorldNameHeight = 36;
        private const int PowerUsageHeight = 37;
        private readonly int _height;

        private List<Command> _commandSequence { get; set; }
        private bool _sequenceReadyToShip { get; set; }

        public Hud(MyAwesomeGame game)
        {
            _game = game;
            _commandSequence = new List<Command>();
            _height = _game.Console.Height - Top;
        }

        public void OnUpdate()
        {
            if (Input.Instance.GetKey(Key.LEFT).Pressed) _commandSequence.Add(new Move(Direction.West));
            if (Input.Instance.GetKey(Key.RIGHT).Pressed) _commandSequence.Add(new Move(Direction.East));
            if (Input.Instance.GetKey(Key.UP).Pressed) _commandSequence.Add(new Move(Direction.North));
            if (Input.Instance.GetKey(Key.DOWN).Pressed) _commandSequence.Add(new Move(Direction.South));
            if (_commandSequence.Count > 0)
            {
                _sequenceReadyToShip = true;
            }
        }
        public void Draw()
        {
            
            for (var x = 0; x < _game.Console.Width; x++)
            for (var y = Top; y < _game.Console.Height; y++)
                _game.Console.Draw(x, y, ' ');

            DrawWorldName();
            DrawPowerUsage();
            _game.Console.DrawLine(0, Top, _game.Console.Width, Top, '▓');
        }

        private void DrawPowerUsage()
        {
            if (_game.Rover.RemainingPower <= 0)
            {
                DrawText("!!! NO POWER !!!", PowerUsageHeight, 1, fgColor: ConsoleColor.Red, bgColor: ConsoleColor.White);
            }
            else
            {
                DrawText("POWER: " + _game.Rover.RemainingPower + "kWh", PowerUsageHeight, 1, fgColor: GetPowerColor(), bgColor: ConsoleColor.White);
            }
        }

        private ConsoleColor GetPowerColor()
        {
            if (_game.Rover.RemainingPower == 0)
            {
                return ConsoleColor.Blue;
            }
            if (_game.Rover.RemainingPower / Rover.MaxPower >= 0.75) return ConsoleColor.Green;
            if (_game.Rover.RemainingPower >= 0.35) return ConsoleColor.Yellow;
            return ConsoleColor.Red;
        }

        private void DrawWorldName()
        {
           DrawText("Now entering:" + _game.World.Name, WorldNameHeight, 0);
            
        }

        public void DrawText(string text, int posY, int startingPosX, Direction direction = Direction.East, ConsoleColor fgColor = ConsoleColor.White, ConsoleColor bgColor = ConsoleColor.Black)
        {
            var xpos = startingPosX;
            foreach (var namecharacter in text)
            {
                _game.Console.Draw(xpos, posY, namecharacter, fgColor, bgColor);
                xpos++;
            }
        }

        public IEnumerable<Command> GetCommands()
        {
            if (_sequenceReadyToShip)
            {
                var temp = _commandSequence.ToList();
                _sequenceReadyToShip = false;
                _commandSequence.Clear();
                return temp;
            }

            return Enumerable.Empty<Command>();
        }
    }
}