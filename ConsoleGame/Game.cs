﻿using System;
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
            : base(new RenderConsole(40, 40) {
                FontWidth = 20,
                FontHeight = 20
            })
        {
            
        }

        protected override void OnInitialize()
        {
            GameTimer.SetInterval(500, () => {
                for (var x = 0; x < 40; x++)
                for (var y = 0; y < 40; y++)
                {
                    _display[x, y] = Chars[Random.Next(0, Chars.Length)];    
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