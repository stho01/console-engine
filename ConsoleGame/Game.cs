using System;
using ConsoleEngine;
using ConsoleEngine.Infrastructure;

namespace ConsoleGame
{
    public class Game : GameBase
    {
        private static readonly char[] Chars = { '*', '#', 'A', '?', 'H' };
        private static readonly Random Random = new(DateTime.Now.Millisecond);
        private readonly char[,] _display = new char[40,40];

        public Game()
            : base(new RenderConsole(40, 40)
            {
                Resizeable = false,
                HideCursor = true,
                FontWidth = 20,
                FontHeight = 20
            })
        {
            
        }

        public override void Initialize()
        {
            GameTimer.SetInterval(500, () => {
                for (var x = 0; x < 40; x++)
                for (var y = 0; y < 40; y++)
                {
                    _display[x, y] = Chars[Random.Next(0, Chars.Length)];    
                }
            });
            
            base.Initialize();
        }

        protected override void Update()
        {
            if (Input.Instance.GetKey(Key.ESCAPE).Pressed) 
                Stop();
        }

        protected override void Render()
        {
            Console.Draw(0,0, _display);
        }
    }
}