using System;
using ConsoleEngine.Infrastructure;
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
#if WINDOWS_DEBUG
                EnableLogger = true
#endif
            };
            
            game.Initialize();
            game.Start();
        }
    }
}