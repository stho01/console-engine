using System;
using System.Collections.Generic;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Windows;

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