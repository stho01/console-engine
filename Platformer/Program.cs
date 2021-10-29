using System;

namespace Platformer
{
    class Program
    {
        static void Main(string[] args)
        {
            var game = new PlatformerGame();
            game.Initialize();
            game.Start();
        }
    }
}