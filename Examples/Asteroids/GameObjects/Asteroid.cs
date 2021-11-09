using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Asteroids.GameObjects
{
    public class Asteroid : GameObject
    {
        private const int Sides = 20; 
        private readonly AsteroidsGame _game;
        private readonly int _radius; 

        private readonly List<Vector2> _vertices = new(); 

        public Asteroid(AsteroidsGame game)
        {
            _game = game;

            _radius = _game.Random.Next(8, 12);
            
            const float angle = (MathF.PI * 2f) / Sides;
            for (var i = 0; i < Sides; i++)
            {
                var x = MathF.Sin(angle * i) * _radius + (float)(_game.Random.NextDouble() * 2f - 1f);
                var y = MathF.Cos(angle * i) * _radius + (float)(_game.Random.NextDouble() * 2f - 1f);
                _vertices.Add(new Vector2(x, y));
            }
        }

        public int Radius => _radius;

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

        public bool Intersects(Vector2 p, float radius)
        {
            var displacement = (Position - p);
            var distanceSquared = displacement.LengthSquared();

            return distanceSquared <= (_radius * _radius) + (radius * radius);
        }
    }
}