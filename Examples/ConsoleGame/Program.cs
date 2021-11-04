using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Native;

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