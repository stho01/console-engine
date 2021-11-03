using System;
using ConsoleEngine;
using ConsoleEngine.Collections;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Windows;

namespace Snake
{
    public enum Direction
    {
        North = 1,
        East = 2,
        South = 3,
        West = 4
    }
    
    public class SnakeGame : GameBase
    {
        private readonly Deque<(int x, int y)> _snake = new();
        private double _time = 0f;
        private const int UpdateThreshold = 150; // ms
        private (int x, int y)? _cleared = null;
        private Direction _direction = Direction.East;
        private (int x, int y) _food;
        private static readonly Random Random = new((int)DateTime.Now.Ticks);
        
        public SnakeGame() 
            : base(new RenderConsole(new ConsoleHandler(30, 30, new FontInfo {
                FontWidth = 14,
                FontHeight = 14,
                FontFace = "Consolas"
            }))) {}

        protected override void OnInitialize()
        {
            ClearScreenOnEachFrame = false;
            
            for (var i = 5; i >= 0; i--) { 
                _snake.PushBack((i, 0));
            }
            
            SpawnFood();
        }

        protected override void OnUpdate()
        {
            _time += GameTime.DeltaTimeMilliseconds;

            if (Input.Instance.GetKey(Key.A).Pressed) _direction = _direction != Direction.East  ? Direction.West  : _direction;
            if (Input.Instance.GetKey(Key.D).Pressed) _direction = _direction != Direction.West  ? Direction.East  : _direction;
            if (Input.Instance.GetKey(Key.W).Pressed) _direction = _direction != Direction.South ? Direction.North : _direction;
            if (Input.Instance.GetKey(Key.S).Pressed) _direction = _direction != Direction.North ? Direction.South : _direction;
            
            if (_time >= UpdateThreshold)
            {
                var x = _direction == Direction.East ? 1
                         : _direction == Direction.West ? -1
                         : 0;
                
                var y = _direction == Direction.South ? 1
                         : _direction == Direction.North ? -1
                         : 0;
                
                _cleared = _snake.PopBack();
                _snake.PushFront((_snake.Front.x + x, _snake.Front.y + y));
                
                if (_snake.Front.x == _food.x && _snake.Front.y == _food.y) {
                    _snake.PushBack((_snake.Back.x, _snake.Back.y));
                    SpawnFood();
                }
                
                _time = 0f;
            }
        }

        protected override void OnRender()
        {
            Console.Draw(_food.x, _food.y, '@');
            
            foreach (var part in _snake) {
                var color = _snake.Front == part ? ConsoleColor.Red : ConsoleColor.White; 
                Console.Draw(part.x, part.y, '█', color);
            }

            if (_cleared.HasValue) {
                Console.Draw(_cleared.Value.x, _cleared.Value.y, ' ');
                _cleared = null;
            }
        }

        private void SpawnFood()
        {
            var counter = 0;
            do
            {
                _food = (
                    Random.Next(0, Console.Width - 1),
                    Random.Next(0, Console.Width - 1)
                );
                counter++;
            } while (counter <= Console.Area && Console.GetCharAt(_food.x, _food.y) != ' ');
        }
    }
}