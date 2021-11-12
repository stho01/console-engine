using ConsoleEngine;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class MyAwesomeGame : GameBase
    {
        private Vector2 _objectPosition;
        
        public MyAwesomeGame() : base(
            width: 40,
            height: 40,
            fontWidth: 14,
            fontHeight: 14)
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
            
        }
    }
}