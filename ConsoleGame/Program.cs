using System;
using System.Linq;
using ConsoleEngine;
using ConsoleEngine.Infrastructure;

namespace ConsoleGame
{
    class Program
    {
        static void Main(string[] args)
        {
            var game = new Game { ShowFps = true };
            game.Initialize();
            game.Start();
        }
    }
}