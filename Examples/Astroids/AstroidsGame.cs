using System.Collections.Generic;
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

        public AstroidsGame() : base(200, 200, 4, 4) {}

        public Camera Camera => _camera;
        public bool IsDebugMode { get; set; } = true;
        
        protected override void OnInitialize() {
            _player = new Player(this) {
                Position = new Vector2(10, 10)
            };

            _camera = new Camera(this);
            _camera.Follow(_player);

            _astroids.Add(new Astroid(this) { Position = new Vector2(20, 20)});
            _astroids.Add(new Astroid(this) { Position = new Vector2(160, 36)});
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
            _player.Draw();
            foreach (var astroid in _astroids)
                astroid.Draw();
        }
    }
}