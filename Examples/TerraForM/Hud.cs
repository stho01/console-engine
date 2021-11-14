using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using TerraForM.Assets;
using TerraForM.Assets.Texts;
using TerraForM.Commands;

namespace TerraForM
{
    public class Hud
    {
        //**********************************************************
        //** constants
        //**********************************************************
        private readonly TerraformGame _game;
        private const int ScoreHeight = 0;
        private const int Top = 35;
        private const int WorldNameHeight = 36;
        private const int PowerUsageHeight = 37;
        private const int DamageTakenHeight = 39;
        private const int AcceleratorsPlantedHeight = 42;
        private const int RemainingSequencesHeight = 44;
        private const int CommandSequenceHeight = 46;
        private const ConsoleColor HudBackgroundColor = ConsoleColor.Black;
        
        //**********************************************************
        //** fields
        //**********************************************************
        
        private readonly int _height;
        private List<Command> _commandSequence { get; set; }
        private bool _sequenceReadyToShip { get; set; }

        //**********************************************************
        //** ctor
        //**********************************************************
        public Hud(TerraformGame game)
        {
            _game = game;
            _commandSequence = new List<Command>();
            _height = _game.Console.Height - Top;
        }

        //**********************************************************
        //** public methods
        //**********************************************************
        
        public void OnUpdate()
        {
           
            if (Input.Instance.GetKey(Key.LEFT).Pressed) _commandSequence.Add(new Move(Direction.West));
            if (Input.Instance.GetKey(Key.RIGHT).Pressed) _commandSequence.Add(new Move(Direction.East));
            if (Input.Instance.GetKey(Key.UP).Pressed) _commandSequence.Add(new Move(Direction.North));
            if (Input.Instance.GetKey(Key.DOWN).Pressed) _commandSequence.Add(new Move(Direction.South));
            if(Input.Instance.GetKey(Key.P).Pressed) _commandSequence.Add(new Plant());
            if (_commandSequence.Count > 0)
            {
                _sequenceReadyToShip = true;
            }
        }
        public void Draw()
        {
            if (_game.GameOver)
            {
                DrawGameOver();
                return;
            }
            
            for (var x = 0; x < _game.Console.Width; x++)
            for (var y = Top; y < _game.Console.Height; y++)
                _game.Console.Draw(x, y, ' ', backgroundColor: HudBackgroundColor);

            DrawWorldName();
            DrawAcceleratorsPlanted();
            DrawPowerUsage();
            DrawDamageTaken();
            DrawRemainingSequences();
            DrawMoveSequence();
            DrawGameScore();
            
            _game.Console.DrawLine(0, Top, _game.Console.Width, Top, '╍');
        }
        
        public void DrawText(string text, int startPosY, int startPosX, ConsoleColor fgColor, ConsoleColor bgColor = HudBackgroundColor ,Direction direction = Direction.East)
        {
            foreach (var character in text)
            {
                _game.Console.Draw(startPosX, startPosY, character, fgColor, bgColor);
                if (direction == Direction.East) startPosX++;
                if (direction == Direction.West) startPosX--;
                if (direction == Direction.South) startPosY++;
                if (direction == Direction.North) startPosY--;
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

        //**********************************************************
        //** private methods
        //**********************************************************
        
        private void DrawRemainingSequences() 
            => DrawText($"{HudTexts.RemainingCommandSequences}: {_game.Rover.RemainingSequences}", RemainingSequencesHeight, 1,ConsoleColor.Blue);

        private void DrawAcceleratorsPlanted()
            => DrawText($"{HudTexts.AtmosphereGeneratorsPlanted}: {_game.Rover.AthmosphereGeneratorsPlanted}", AcceleratorsPlantedHeight, 1,ConsoleColor.Blue);
        private void DrawGameOver()
            => _game.Console.Draw(
                x: (int)_game.Console.ScreenCenter.X - (Sprites.Sprite.Width / 2),
                y: (int)_game.Console.ScreenCenter.Y - Sprites.Sprite.Height / 2,
                sprite: Sprites.Sprite
            );

        private void DrawMoveSequence()
        {
            if (_commandSequence.Any())
            {
                var visualCommandSequenceRepresentation =
                    $"{HudTexts.QueuedCommands} " + 
                    String.Join(' ', _commandSequence.Select(c => c.GetVisualRepresentation()));
                DrawText(visualCommandSequenceRepresentation, CommandSequenceHeight, 0, ConsoleColor.Magenta );
            }
        }

        private void DrawPowerUsage()
        {
            if (_game.Rover.PowerDepleted())
            {
                DrawText($"{HudTexts.NoPower}", PowerUsageHeight, 1, fgColor: ConsoleColor.Red, bgColor: ConsoleColor.White);
            }
            else
            {
                DrawText($"{HudTexts.Power}: {_game.Rover.RemainingPower.ToString("N")} {HudTexts.PowerUnit}", PowerUsageHeight, 1, fgColor: GetPowerColor());
            }
        }

        private void DrawDamageTaken()
            => DrawText($"{HudTexts.DamageTaken}: {_game.Rover.DamageTaken}", DamageTakenHeight, 1,GetDamageTakenColor());
        
        private void DrawWorldName()
           => DrawText($"{HudTexts.MapPrefix}:  {_game.World.Name}", WorldNameHeight, 0, ConsoleColor.DarkGreen );

        private void DrawGameScore()
        {
            var scoreText = $"{HudTexts.Score}:";
            var scoreValueText = _game.Score.ToString();
            DrawText("____________________", ScoreHeight, _game.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
            DrawText(scoreText, ScoreHeight+1, _game.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
            DrawText(scoreValueText, ScoreHeight+1, _game.Console.Width - scoreValueText.Length, ConsoleColor.DarkGreen);
            DrawText("____________________", ScoreHeight+2, _game.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
            DrawText("                    ", ScoreHeight+3, _game.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
            
        }

        private ConsoleColor GetPowerColor()
        {
            if (_game.Rover.RemainingPower >= ((_game.Rover.MaxPower / 4) * 3)) return ConsoleColor.Green;
            if (_game.Rover.RemainingPower >= ((_game.Rover.MaxPower / 4) * 2)) return ConsoleColor.Yellow;
            if (_game.Rover.RemainingPower >= ((_game.Rover.MaxPower / 4) * 1)) return ConsoleColor.Red;
            return ConsoleColor.Red;
        }
        
        private ConsoleColor GetDamageTakenColor()
        {
            if (_game.Rover.DamageTaken > 75) return ConsoleColor.Red;
            if (_game.Rover.DamageTaken > 50) return ConsoleColor.Yellow;
            if (_game.Rover.DamageTaken > 25) return ConsoleColor.DarkGreen;
            return ConsoleColor.Green;
        }
    }
}