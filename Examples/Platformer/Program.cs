using System;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Native;

namespace Platformer
{
    class Program
    {
        static void Main(string[] args)
        {
            Input.Instance.SetHandler(new InputHandler());

            var game = new PlatformerGame {
                EnableLogger = true
            };
            
            game.Initialize();
            game.Start();
            Console.ReadKey();
        }
    }
}