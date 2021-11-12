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
                Position = new Point(10, 10)
            };

            Hud = new Hud(this);
            Camera = new Camera(this);
            Camera.Follow(Rover);
            
            _obstacles.Add(new Rock(this) {
                Position = new Point(10, 10)
            });
        }

        protected override void OnUpdate()
        {
            Camera.Update();
            
            if (Input.Instance.GetKey(Key.A).Pressed) Rover.Position += new Point(-2, 0);
            if (Input.Instance.GetKey(Key.D).Pressed) Rover.Position += new Point(2, 0);
            if (Input.Instance.GetKey(Key.W).Pressed) Rover.Position += new Point(0, -2);
            if (Input.Instance.GetKey(Key.S).Pressed) Rover.Position += new Point(0, 2);
            
            var commands = Hud.GetCommands();
            Rover.DoCommands(commands);
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