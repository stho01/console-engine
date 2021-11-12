using System.Collections.Generic;
using System.Linq;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using Microsoft.Xna.Framework;
using MyAwesomeConsoleGame.Entities;
using MyAwesomeConsoleGame.Entities.Tiles;

namespace MyAwesomeConsoleGame
{
    public class MyAwesomeGame : GameBase
    {
        public Rover Rover;
        public Hud Hud;
        public Camera Camera;
        public Queue<Command> _currentCommands = new Queue<Command>();
        public World World;
        public WorldLoader Loader;


        public MyAwesomeGame() : base(
            width: 70,
            height: 50,
            fontWidth: 10,
            fontHeight: 10) {
            
        }

        protected override void OnInitialize()
        {
            Loader = new WorldLoader(this);
            
            Rover = new Rover(this) {
                Position = new Vector2(10, 10)
            };

            Hud = new Hud(this);
            Camera = new Camera(this);
            Camera.Follow(Rover);
       
            World = Loader.LoadWorld("maps/map3.txt");
        }

        protected override void OnUpdate()
        {
            Hud.OnUpdate();
            if (Input.Instance.GetKey(Key.A).Held) Rover.MoveWest();
            if (Input.Instance.GetKey(Key.D).Held) Rover.MoveEast();
            if (Input.Instance.GetKey(Key.W).Held) Rover.MoveNorth();
            if (Input.Instance.GetKey(Key.S).Held) Rover.MoveSouth();

            if (Input.Instance.GetKey(Key.SPACE).Pressed && !_currentCommands.Any())
            {
                var commands = Hud.GetCommands();
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
           
            // Rover.DoCommands(commands);
            
            Rover.Update();
            Camera.Update();
        }
        
        protected override void OnRender()
        {
            World.Draw();
            Rover.Draw();
            Hud.Draw();
        }
    }
}