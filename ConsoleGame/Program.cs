using ConsoleEngine.Infrastructure.Inputs;
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