using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Microsoft.Xna.Framework;
using TerraForM.Commands;
using TerraForM.GameObjects;

namespace TerraForM
{
    public enum GameStates
    {
        InputName,
        Menu,
        Playing,
        GameOver
    }

    public class TerraformGame : GameBase
    {

        //**********************************************************
        //** props: 
        //**********************************************************

        public Rover Rover;
        public Hud Hud;
        public Camera Camera;
        public Queue<Command> _currentCommands = new();
        public World World;
        public string Playername;
        public int Score;
        public GameStates GameState = GameStates.Menu;
        public readonly List<PlantEmitter> PlantEmitters = new();
        public int CurrentMap = 0;
        public bool GameOver;
        public bool IsDebugMode;

        public string[] Maps =
        {
            "Assets/Maps/map1.txt",
            "Assets/Maps/map2.txt",
            "Assets/Maps/map3.txt",
            "Assets/Maps/map4.txt"
        };

        //**********************************************************
        //** ctor:
        //**********************************************************

        public TerraformGame() : base(
            width: 72,
            height: 50,
            fontWidth: 15,
            fontHeight: 15)
        {
            Console.ClearColor = ConsoleColor.Black;
        }

        //**********************************************************
        //** public methods:
        //**********************************************************

        public void RotateMap()
        {
            CurrentMap++;
            if (CurrentMap >= Maps.Length)
            {
                CurrentMap = 0;
            }

            StartNewGame();

        }
        
        protected override void OnInitialize()
        {
            GameState = GameStates.InputName;
            Camera = new Camera(this);
            StartNewGame();

            Hud = new Hud(this);
            Camera.Follow(Rover);
        }

        //**********************************************************
        //** overrides:
        //**********************************************************

        protected override void OnUpdate()
        {
            switch (GameState)
            {
                case GameStates.InputName:
                    HandleNameInput();
                    break;
                case GameStates.Menu:
                    if (Input.Instance.GetKey(Key.H).Pressed) GameState = GameStates.Playing;
                    break;
                case GameStates.Playing:
                    if (Input.Instance.GetKey(Key.F1).Pressed) IsDebugMode = !IsDebugMode;

                    if (Rover.PowerDepleted() || Rover.RemainingSequences < 0)
                    {
                        GameOver = true;
                    }

                    Hud.OnUpdate();

                    if (Input.Instance.GetKey(Key.ESCAPE).Held) GameState = GameStates.Menu;

                    if (IsDebugMode)
                    {
                        if (Input.Instance.GetKey(Key.A).Held) Rover.MoveWest();
                        if (Input.Instance.GetKey(Key.D).Held) Rover.MoveEast();
                        if (Input.Instance.GetKey(Key.W).Held) Rover.MoveNorth();
                        if (Input.Instance.GetKey(Key.S).Held) Rover.MoveSouth();
                    }
                   
                    if (Input.Instance.GetKey(Key.R).Pressed) StartNewGame();
                    
                    HandleQueuedCommands();
                    
                    Rover.Update();
                    PlantEmitters.ForEach(e => e.Update());

                    break;
                case GameStates.GameOver:
                    break;
                default:
                    break;
            }

            Camera.Update();
            
        }

        protected override void OnRender()
        {
            World.Draw();
            if (GameState == GameStates.Playing)
            {
                PlantEmitters.ForEach(e => e.Draw());
                Rover.Draw();
                Hud.Draw();
                if (IsDebugMode)
                {
                    Console.Draw(0, 0, $"Pos  : {Rover.Position}");
                    Console.Draw(0, 1, $"SPos : {Rover.GetScreenPos()}");
                    Console.Draw(0, 2, $"BB   : {Rover.BoundingBox}");
                }
            }            
        }

        //**********************************************************
        //** private methods:
        //**********************************************************

        private void StartNewGame()
        {
            GameOver = false;
            Score = 0;
            Camera.Position = Vector2.Zero;
            World = WorldLoader.LoadWorld(this, Maps[CurrentMap]);
            Rover = new Rover(this)
            {
                Position = World.StartingPoint.Position,
                Velocity = Vector2.Zero
            };
            Camera.Follow(Rover);
            PlantEmitters.Clear();
        }
        
        private void HandleQueuedCommands()
        {
            if (Input.Instance.GetKey(Key.SPACE).Pressed && !_currentCommands.Any())
            {
                var commands = Hud.GetCommands();
                if (commands.Any())
                {
                    Rover.RemainingSequences--;
                }

                foreach (var command in commands)
                    _currentCommands.Enqueue(command);
            }

            if (_currentCommands.Any())
            {
                var currentCommand = _currentCommands.Peek();
                currentCommand.Update(Rover);
                if (currentCommand.IsDone())
                {
                    _currentCommands.Dequeue();
                }
            }
        }

        private void HandleNameInput()
        {
            if (Input.Instance.GetKey(Key.ENTER).Pressed) GameState = GameStates.Menu;
            var pressedSpace09AZkeys = Input.Instance.GetPressedKeyCodesSpace09AZ().Select(kc => (char)kc);
            if (pressedSpace09AZkeys?.Any() == true)
                Playername += string.Concat(pressedSpace09AZkeys);
            if (Input.Instance.GetKey(Key.BACKSPACE).Pressed && !string.IsNullOrEmpty(Playername))
                Playername = Playername.Substring(Playername.Length - 1);
        }
    }
}