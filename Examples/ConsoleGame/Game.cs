using System;
using ConsoleEngine;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Native;

namespace ConsoleGame
{
    public class Game : GameBase
    {
        private static readonly char[] Chars = { '*', '#', 'A', '?', 'H' };
        private static readonly ConsoleColor[] Colors = { ConsoleColor.Cyan, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.White, ConsoleColor.DarkGray };
        private static readonly Random Random = new(DateTime.Now.Millisecond);
        private const int Width = 40;
        private const int Height = 40;
        private readonly Pixel[,] _display = new Pixel[Width,Height];

        public Game()
            : base(new RenderConsole(
                new ConsoleHandler(Width, Height, new FontInfo {
                    FontWidth = 8,
                    FontHeight = 8,
                    FontFace = "Consolas"
                }))) {}

        protected override void OnInitialize()
        {
            // callback called every 500ms
            GameTime.SetInterval(500, () => {
                for (var x = 0; x < Width; x++)
                for (var y = 0; y < Height; y++)
                {
                    _display[x, y].Char = '█'; //Chars[Random.Next(0, Chars.Length)];
                    _display[x, y].ForegroundColor = Colors[Random.Next(0, Colors.Length)];
                }
            });
        }

        protected override void OnUpdate()
        {
            if (Input.Instance.GetKey(Key.ESCAPE).Pressed) 
                Stop();
        }

        protected override void OnRender()
        {
            Console.Draw(0,0, _display);
        }
    }
}