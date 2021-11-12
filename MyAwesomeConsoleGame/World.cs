using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using MyAwesomeConsoleGame.Entities.Tiles;
using MyAwesomeConsoleGame.Sprites;

namespace MyAwesomeConsoleGame
{
    public class World
    {
        private readonly MyAwesomeGame _game;
        private readonly MapTile[,] _tiles;
        
        private static readonly Sprite BorderSprite = Sprite.FromStringArray(new []{
            "X▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.Magenta);
      

        private readonly int _mapWidth;
        private readonly int _mapHeight;
        public const int TileSize = 3;
        public bool DrawStory = false;
        
        public World(MyAwesomeGame game, string name, MapTile[,] map) {
            _game = game;
            _tiles = map;
            Name = name;
            _mapWidth = map.GetLength(0);
            _mapHeight = map.GetLength(1);
        }

        public string Name { get; set; }
        public int Sequences { get; set; }
        
        public int Width => _mapWidth;
        public int Height => _mapHeight;
        public string[] Map { get; }

        public void Draw()
        {
            if (DrawStory)
            {
                _game.Console.Draw(0,0,Story.Sprite);
                return;
            }
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                _tiles[x, y]?.Draw();
            }
            
            for (var x = -1; x < Width + 1; x++)
            {
                var topPos = _game.Camera.WorldToScreenPos(new Vector2(x * TileSize,-1*TileSize));
                var bottomPos = _game.Camera.WorldToScreenPos(new Vector2(x * TileSize, Height*TileSize));
                
                _game.Console.Draw((int)topPos.X, (int)topPos.Y, BorderSprite);
                _game.Console.Draw((int)bottomPos.X, (int)bottomPos.Y, BorderSprite);
            }
            
            for (var y = -1; y < Height + 1; y++)
            {
                var leftPos = _game.Camera.WorldToScreenPos(new Vector2(-1*TileSize, y * TileSize));
                var rightPos = _game.Camera.WorldToScreenPos(new Vector2(Width*TileSize, y * TileSize));
                
                _game.Console.Draw((int)leftPos.X, (int)leftPos.Y, BorderSprite);
                _game.Console.Draw((int)rightPos.X, (int)rightPos.Y, BorderSprite);
            }
        }

        public bool Intersects(GameObject gameObject, out List<GameObject> with)
        {
            var intersects = false;
            with = null;
            
            foreach (var tile in _tiles)
            {
                if (tile?.BoundingBox.Intersects(gameObject.BoundingBox) == true)
                {
                    with ??= new List<GameObject>();
                    with.Add(tile);
                    intersects = true;
                }
            }

            return intersects;
        }
    }
}
