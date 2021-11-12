using System.Collections.Generic;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class MyAwesomeGame : GameBase
    {
        public Rover Rover;
        public Hud Hud;
        public Camera Camera;
        public List<Obstacle> _obstacles = new List<Obstacle>(); 
            
        public MyAwesomeGame() : base(
            width: 70,
            height: 50,
            fontWidth: 10,
            fontHeight: 10) {
            
        }

        protected override void OnInitialize()
        {
            Rover = new Rover(this) {
                Position = new Vector2(10, 10)
            };

            Hud = new Hud(this);
            Camera = new Camera(this);
            Camera.Follow(Rover);
            
            _obstacles.Add(new Rock(this) {
                Position = new Vector2(10, 10)
            });
        }

        protected override void OnUpdate()
        {
            if (Input.Instance.GetKey(Key.A).Held) Rover.MoveWest();
            if (Input.Instance.GetKey(Key.D).Held) Rover.MoveEast();
            if (Input.Instance.GetKey(Key.W).Held) Rover.MoveNorth();
            if (Input.Instance.GetKey(Key.S).Held) Rover.MoveSouth();
            
            // var commands = Hud.GetCommands();
            // Rover.DoCommands(commands);
            
            Rover.Update();
            Camera.Update();
        }
        
        protected override void OnRender()
        {
            foreach (var obstacle in _obstacles)
                obstacle.Draw();
            Rover.Draw();
            Hud.Draw();
        }
    }
}