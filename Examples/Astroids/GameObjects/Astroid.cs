using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Astroids.GameObjects
{
    public class Astroid : GameObject
    {
        private const int Sides = 20; 
        private readonly AstroidsGame _game;
        private static readonly Random _random = new((int)DateTime.Now.Ticks);

        private readonly List<Vector2> _vertices = new(); 

        public Astroid(AstroidsGame game)
        {
            _game = game;

            var size = _random.Next(8, 12);
            
            var angle = (MathF.PI * 2f) / Sides;
            for (int i = 0; i < Sides; i++)
            {
                var x = MathF.Sin(angle * i) * size + (float)(_random.NextDouble() * 2f - 1f);
                var y = MathF.Cos(angle * i) * size + (float)(_random.NextDouble() * 2f - 1f);
                _vertices.Add(new Vector2(x, y));
            }
        }

        public void Draw()
        {
            for (var i = 0; i < Sides; i++)
            {
                var screenPos = _game.Camera.WorldToScreenPos(Position);
                var p1 = _vertices[i] + screenPos;
                var p2 = _vertices[(i + 1) % Sides] + screenPos;
                
                _game.Console.DrawLine(p1.ToPoint(), p2.ToPoint(), '#', ConsoleColor.Green);
            }
        }
    }
}