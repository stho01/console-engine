using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Microsoft.Xna.Framework;
using TerraForM.Commands;
using TerraForM.GameObjects;

namespace TerraForM.Scenes
{
    public class GameScene : Scene<TerraformGame>
    {
        //**********************************************************
        //** fields
        //**********************************************************

        private const string MapAssetPath = "Assets/Maps";
        private readonly string _worldName;
        private readonly Queue<Command> _currentCommands = new();
              
        //**********************************************************
        //** ctor
        //**********************************************************
        
        public GameScene(string worldName)
        {
            _worldName = worldName ?? throw new ArgumentNullException(nameof(worldName));
        }
              
        //**********************************************************
        //** props:
        //**********************************************************

        public virtual World World { get; private set; }
        public Rover Rover { get; private set; }
        public Hud Hud { get; private set; }
        public bool GameOver { get; set; }
        public List<PlantEmitter> PlantEmitters { get; } = new();

        //**********************************************************
        //** public methods:
        //**********************************************************
        
        public override void OnLoad()
        {
            Game.Camera.Position = Vector2.Zero;
            World = WorldLoader.LoadWorld(Game, $"{MapAssetPath}/{_worldName}.txt");
            Rover = new Rover(this) {
                Position = World.StartingPoint.Position,
                Velocity = Vector2.Zero
            };
            Rover.OnFinishReached += RoverOnOnFinishReached;
            Rover.OnAthmosphereGeneratorsPlanted += RoverOnOnAthmosphereGeneratorsPlanted;
            Hud = new Hud(this);
            Game.Camera.Follow(Rover);
        }

        public override void OnUnload()
        {
            World = null;
            Rover.OnFinishReached -= RoverOnOnFinishReached;
            Rover.OnAthmosphereGeneratorsPlanted -= RoverOnOnAthmosphereGeneratorsPlanted;
            Rover = null;
            _currentCommands.Clear();
            PlantEmitters.Clear();
        }

        public override void OnUpdate()
        {
            GameOver = Rover.PowerDepleted() || Rover.RemainingSequences < 0;
            
            Hud.OnUpdate();
            
            if (Input.Instance.GetKey(Key.ESCAPE).Held) 
                Game.Scenes.Push<MenuScene>();

            HandleDebugInput();

            if (Input.Instance.GetKey(Key.SPACE).Pressed && !_currentCommands.Any()) 
                CommitCommands();
            
            UpdateCurrentCommand();
            
            Rover.Update();
            PlantEmitters.ForEach(e => e.Update());
            
            if (Input.Instance.GetKey(Key.R).Pressed) 
                Game.Scenes.Set(new GameScene(_worldName));                
        }
        
        private void HandleDebugInput()
        {
            if (!Game.IsDebugMode) return;
            if (Input.Instance.GetKey(Key.A).Held) Rover.MoveWest();
            if (Input.Instance.GetKey(Key.D).Held) Rover.MoveEast();
            if (Input.Instance.GetKey(Key.W).Held) Rover.MoveNorth();
            if (Input.Instance.GetKey(Key.S).Held) Rover.MoveSouth();
        }

        private void CommitCommands()
        {
            var commands = Hud.GetCommands().ToList();
            if (commands.Any())
            {
                Rover.RemainingSequences--;
            }

            foreach (var command in commands)
                _currentCommands.Enqueue(command);
        }
        
        private void UpdateCurrentCommand()
        {
            if (!_currentCommands.Any()) return;
            
            var currentCommand = _currentCommands.Peek();
            currentCommand.Update(Rover);
            if (currentCommand.IsDone())
            {
                _currentCommands.Dequeue();
            }
        }

        public override void OnRender()
        {
            World.Draw();
            PlantEmitters.ForEach(e => e.Draw());
            Rover.Draw();
            Hud.Draw();
            
            RenderDebugInfo();
        }

        private void RenderDebugInfo()
        {
            if (!Game.IsDebugMode) 
                return;
            
            Game.Console.Draw(0, 0, $"Pos  : {Rover.Position}");
            Game.Console.Draw(0, 1, $"SPos : {Rover.GetScreenPos()}");
            Game.Console.Draw(0, 2, $"BB   : {Rover.BoundingBox}");
        }
        
              
        //**********************************************************
        //** event handlers:
        //**********************************************************
        
        private void RoverOnOnAthmosphereGeneratorsPlanted(object sender, EventArgs e)
        {
            PlantEmitters.Add(new PlantEmitter(this) {
                Position = Rover.Position
            });
        }

        private void RoverOnOnFinishReached(object sender, EventArgs e)
        {
            Game.Score += (int)Rover.RemainingPower - (Rover.DamageTaken);
            Game.Score += (Rover.AthmosphereGeneratorsPlanted * 10000);
            Game.RotateMap();
        }
    }
}