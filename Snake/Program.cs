using System;
using System.Collections.Generic;

namespace Snake
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var game = new SnakeGame {
                ShowFps = true
            };
            game.Initialize();
            game.Start();
        }
    }
}