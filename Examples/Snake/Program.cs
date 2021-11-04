using System;
using System.Collections.Generic;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Native;

namespace Snake
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Input.Instance.SetHandler(new InputHandler());
            
            var game = new SnakeGame {
                ShowFps = true
            };
            game.Initialize();
            game.Start();
        }
    }
}