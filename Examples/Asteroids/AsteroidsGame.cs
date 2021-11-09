using System;
using System.Collections.Generic;
using System.Linq;
using Asteroids.GameObjects;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Microsoft.Xna.Framework;

namespace Asteroids
{
    public class AsteroidsGame : GameBase
    {
        private Player _player;
        private Camera _camera;
        private readonly HashSet<Asteroid> _asteroids = new();
        private static readonly Random _random = new((int)DateTime.Now.Ticks);

        public AsteroidsGame() : base(200, 200, 4, 4) { }

        public Camera Camera => _camera;
        public Random Random => _random;
        
        protected override void OnInitialize() {
            _player = new Player(this) {
                Position = new Vector2(10, 10),
                Thrust = 250f
            };

            _camera = new Camera(this);
            _camera.Follow(_player);

            for (var i = 0; i < 50; i++)
            {
                var asteroid = new Asteroid(this);

                do // dont spawn on top of each other. 
                {
                    asteroid.Position = new Vector2(
                        _random.Next(-Console.Width, Console.Width * 2),
                        _random.Next(-Console.Height, Console.Height * 2)
                    );
                } while (_asteroids.Any(x => x.Intersects(asteroid.Position, asteroid.Radius)));

                _asteroids.Add(asteroid);
            }
        }

        protected override void OnUpdate()
        {
            if (Input.Instance.GetKey(Key.A).Held) _player.SteerLeft();
            if (Input.Instance.GetKey(Key.D).Held) _player.SteerRight();
            if (Input.Instance.GetKey(Key.W).Held) _player.Move();
            
            _camera.Update();
            _player.Update();
        }

        protected override void OnRender()
        {
            foreach (var asteroid in _asteroids)
                asteroid.Draw();
            
            _player.Draw();
        }
    }
}