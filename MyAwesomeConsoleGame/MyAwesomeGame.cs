using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Microsoft.Xna.Framework;
using MyAwesomeConsoleGame.Entities;
using MyAwesomeConsoleGame.Entities.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyAwesomeConsoleGame
{
    public enum GameStates
    {
        InputName,
        Menu,
        Playing,
        GameOver
    }

    public class MyAwesomeGame : GameBase
    {
        public Rover Rover;
        public Hud Hud;
        public Camera Camera;
        public Queue<Command> _currentCommands = new();
        public World World;
        public string Playername;
        public int Score;
        public GameStates GameState = GameStates.Menu;
        public bool DrawStory = true;
        public readonly List<PlantEmitter> PlantEmitters = new();

        public string[] Maps =
        {
            // "Assets/Maps/map0.txt",
            "Assets/Maps/map1.txt",
            "Assets/Maps/map2.txt",
            "Assets/Maps/map3.txt",
            "Assets/Maps/map4.txt"
        };

        public int CurrentMap = 0;

        public bool GameOver;
        public bool IsDebugMode { get; set; }


        public MyAwesomeGame() : base(
            width: 72,
            height: 50,
            fontWidth: 15,
            fontHeight: 15)
        {
            Console.ClearColor = ConsoleColor.Black;
        }

        protected override void OnInitialize()
        {
            ///System.Console.Write("What's your name?");
            //Playername = System.Console.ReadLine();
            GameState = GameStates.InputName;
            Camera = new Camera(this);
            StartNewGame();

            Hud = new Hud(this);
            Camera.Follow(Rover);

            new Task(async () =>
            {
                await Music.PlayIntroMusic();
            }).Start();
        }

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

        protected override void OnUpdate()
        {
            switch (GameState)
            {
                case GameStates.InputName:
                    if (Input.Instance.GetKey(Key.ENTER).Pressed) GameState = GameStates.Menu;
                    Playername = "Bob Kåre ??? Junior";
                    break;
                case GameStates.Menu:
                    if (Input.Instance.GetKey(Key.H).Pressed) GameState = GameStates.Playing;
                    break;
                case GameStates.Playing:
                    if (Input.Instance.GetKey(Key.F1).Pressed) IsDebugMode = !IsDebugMode;

                    if (Rover.RemainingPower <= 0 || Rover.RemainingSequences < 0)
                    {
                        GameOver = true;
                        new Task(async () =>
                        {
                            await Music.PlayGameOverMusic();
                        }).Start();
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

        public void RotateMap()
        {
            CurrentMap++;
            if (CurrentMap >= Maps.Length)
            {
                CurrentMap = 0;
            }

            StartNewGame();

        }
    }
}