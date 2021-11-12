using ConsoleEngine;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class MyAwesomeGame : GameBase
    {
        private Vector2 _objectPosition;
        
        public MyAwesomeGame() : base(
            width: 140,
            height: 100,
            fontWidth: 10,
            fontHeight: 10)
        {
            
        }

        protected override void OnInitialize()
        {
            _objectPosition = Console.ScreenCenter;
        }

        protected override void OnUpdate()
        {
            // GameTime holder info om tid siden forrige frame 
            var timeSinceLastFrame = GameTime.Delta.TotalSeconds;
            
            
        }
        
        protected override void OnRender()
        {
            var offset = 30;
            Console.DrawLine(0, Console.Height - offset, Console.Width, Console.Height - offset, '#');
        }
    }
}