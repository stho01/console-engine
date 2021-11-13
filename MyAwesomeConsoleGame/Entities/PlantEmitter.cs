using System;
using System.Collections.Generic;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame.Entities
{
    public class PlantEmitter : GameObject
    {
        public const float LifeTime = 1000;
        private float _ellapsed = 0f;
        private const float EmitFreq = 150;
        private float _emitTimer = 0f;
        private readonly List<Vector2> _pixels = new();
        private int _iteration = 0;
        private static readonly Random _random = new();

        private readonly Pixel Grass = new Pixel
        {
            Char = 'v',
            ForegroundColor = ConsoleColor.DarkGreen
        };
        
        public PlantEmitter(MyAwesomeGame game) : base(game)
        {
            
        }

        public void Update()
        {
            if (_ellapsed < LifeTime)
            {
                var dt = (float)GameTime.Delta.TotalMilliseconds;
                _ellapsed += dt;
                _emitTimer += dt;

                if (_emitTimer > EmitFreq)
                {
                    SpawnPixels();
                    _emitTimer = 0f;
                }
            }
        }

        private void SpawnPixels()
        {
            _iteration++;
            
            for (var i = 0; i < 30; i++)
            {
                var x = MathF.Sin((MathF.PI / _random.Next(1, 5))*i);
                var y = MathF.Cos((MathF.PI / _random.Next(1, 5))*i);
                var pos = new Vector2(x, y) * (_iteration + 4);

                pos += Position;
                
                _pixels.Add(pos);
            }
        }

        public void Draw()
        {
            foreach (var pos in _pixels)
            {
                var screenPos = Game.Camera.WorldToScreenPos(pos);
                Game.Console.Draw((int)screenPos.X, (int)screenPos.Y, Grass);
            }
        }
    }
}