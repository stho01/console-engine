using System;
using ConsoleEngine.Collections;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace Platformer.GameObjects
{
    public class UnendingGround
    {
        private readonly PlatformerGame _game;
        private readonly Sprite _tileSprite = Sprite.FromStringArray(new [] {
            "█▒░░",
            "░░██",
            "██▒░",
            "▒░██",
        }, ConsoleColor.Red);
        private readonly Deque<Vector2> _tiles = new();

        public UnendingGround(PlatformerGame game)
        {
            _game = game;
        }
        
        public void Init()
        {
            var numberOfTiles = (_game.Console.Width / _tileSprite.Width) + 1;
            var y = _game.Console.Width - _tileSprite.Height;
            
            for (var i = 0; i < numberOfTiles; i++) {
                _tiles.PushBack(new Vector2(i * _tileSprite.Width, y));
            }
        }

        public void Draw()
        {
            foreach (var tile in _tiles)
            {
                var (x, y) = tile - _game.Camera.Position;

                if ((x + _tileSprite.Width) < 0) {
                    _tiles.PopFront();
                    _tiles.PushBack(new Vector2(_tiles.Back.X + _tileSprite.Width, _tiles.Back.Y));
                }

                if (x > _game.Console.Width) {
                    _tiles.PopBack();
                    _tiles.PushFront(new Vector2(_tiles.Front.X - _tileSprite.Width, _tiles.Front.Y));
                }
                
                _game.Console.Draw((int)x, (int)y, _tileSprite);    
            }
        }
    }
}