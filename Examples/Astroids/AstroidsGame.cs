using System;
using System.Collections.Generic;
using System.Linq;
using Astroids.GameObjects;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using Microsoft.Xna.Framework;

namespace Astroids
{
    public class AstroidsGame : GameBase
    {
        private Player _player;
        private Camera _camera;
        private readonly HashSet<Astroid> _astroids = new();
        private static readonly Random _random = new((int)DateTime.Now.Ticks);

        public AstroidsGame() : base(200, 200, 4, 4) { }

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
                var astroid = new Astroid(this);

                do // dont spawn on top of each other. 
                {
                    astroid.Position = new Vector2(
                        _random.Next(-Console.Width, Console.Width * 2),
                        _random.Next(-Console.Height, Console.Height * 2)
                    );
                } while (_astroids.Any(x => x.Intersects(astroid.Position, astroid.Radius)));

                _astroids.Add(astroid);
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
            foreach (var astroid in _astroids)
                astroid.Draw();
            
            _player.Draw();
        }
    }
}