using System;
using System.Linq;
using ConsoleEngine;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Windows;

namespace ConsoleGame
{
    class Program
    {
        static void Main(string[] args)
        {
            Input.Instance.SetHandler(new InputHandler());
            
            var game = new Game { ShowFps = true };
            game.Initialize();
            game.Start();
        }
    }
}