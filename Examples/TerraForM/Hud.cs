using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using TerraForM.Assets;
using TerraForM.Assets.Texts;
using TerraForM.Commands;
using TerraForM.Scenes;

namespace TerraForM;

public class Hud(GameScene scene)
{
    //**********************************************************
    //** constants
    //**********************************************************

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
        
    private readonly int _height = scene.Game.Console.Height - Top;
    private List<Command> _commandSequence { get; set; } = new();
    private bool _sequenceReadyToShip { get; set; }
    public TerraformGame Game => scene.Game;

    //**********************************************************
    //** ctor
    //**********************************************************

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
        if (scene.GameOver)
        {
            DrawGameOver();
            return;
        }
            
        for (var x = 0; x < Game.Console.Width; x++)
        for (var y = Top; y < Game.Console.Height; y++)
            scene.Console.Draw(x, y, ' ', backgroundColor: HudBackgroundColor);

        DrawWorldName();
        DrawAcceleratorsPlanted();
        DrawPowerUsage();
        DrawDamageTaken();
        DrawRemainingSequences();
        DrawMoveSequence();
        DrawGameScore();
            
        scene.Console.DrawLine(0, Top, Game.Console.Width, Top, '╍');
    }
        
    public void DrawText(string text, int startPosY, int startPosX, ConsoleColor fgColor, ConsoleColor bgColor = HudBackgroundColor ,Direction direction = Direction.East)
    {
        foreach (var character in text)
        {
            scene.Console.Draw(startPosX, startPosY, character, fgColor, bgColor);
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
        => DrawText($"{HudTexts.RemainingCommandSequences}: {scene.Rover.RemainingSequences}", RemainingSequencesHeight, 1,ConsoleColor.Blue);

    private void DrawAcceleratorsPlanted()
        => DrawText($"{HudTexts.AtmosphereGeneratorsPlanted}: {scene.Rover.AthmosphereGeneratorsPlanted}", AcceleratorsPlantedHeight, 1,ConsoleColor.Blue);
       
    private void DrawGameOver()
        => scene.Console.Draw(
            x: (int)scene.Console.ScreenCenter.X - (Sprites.GameOver.Width / 2),
            y: (int)scene.Console.ScreenCenter.Y - Sprites.GameOver.Height / 2,
            sprite: Sprites.GameOver
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
        if (scene.Rover.PowerDepleted())
        {
            DrawText($"{HudTexts.NoPower}", PowerUsageHeight, 1, fgColor: ConsoleColor.Red, bgColor: ConsoleColor.White);
        }
        else
        {
            DrawText($"{HudTexts.Power}: {scene.Rover.RemainingPower.ToString("N")} {HudTexts.PowerUnit}", PowerUsageHeight, 1, fgColor: GetPowerColor());
        }
    }

    private void DrawDamageTaken()
        => DrawText($"{HudTexts.DamageTaken}: {scene.Rover.DamageTaken}", DamageTakenHeight, 1,GetDamageTakenColor());
        
    private void DrawWorldName()
        => DrawText($"{HudTexts.MapPrefix}:  {scene.World.Name}", WorldNameHeight, 0, ConsoleColor.DarkGreen );

    private void DrawGameScore()
    {
        var scoreText = $"{HudTexts.Score}:";
        var scoreValueText = Game.Score.ToString();
        DrawText("____________________", ScoreHeight, scene.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
        DrawText(scoreText, ScoreHeight+1, scene.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
        DrawText(scoreValueText, ScoreHeight+1, scene.Console.Width - scoreValueText.Length, ConsoleColor.DarkGreen);
        DrawText("____________________", ScoreHeight+2, scene.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
        DrawText("                    ", ScoreHeight+3, scene.Console.Width - (scoreText.Length + scoreValueText.Length), ConsoleColor.DarkGray);
            
    }

    private ConsoleColor GetPowerColor()
    {
        if (scene.Rover.RemainingPower >= ((scene.Rover.MaxPower / 4) * 3)) return ConsoleColor.Green;
        if (scene.Rover.RemainingPower >= ((scene.Rover.MaxPower / 4) * 2)) return ConsoleColor.Yellow;
        if (scene.Rover.RemainingPower >= ((scene.Rover.MaxPower / 4) * 1)) return ConsoleColor.Red;
            
        return ConsoleColor.Red;
    }
        
    private ConsoleColor GetDamageTakenColor()
    {
        if (scene.Rover.DamageTaken > 75) return ConsoleColor.Red;
        if (scene.Rover.DamageTaken > 50) return ConsoleColor.Yellow;
        if (scene.Rover.DamageTaken > 25) return ConsoleColor.DarkGreen;
        return ConsoleColor.Green;
    }
}