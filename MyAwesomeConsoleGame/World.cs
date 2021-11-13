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
        private readonly List<MapTile> _border = new();
        private readonly int _mapWidth;
        private readonly int _mapHeight;
        public const int TileSize = 5;
        
        
        public World(MyAwesomeGame game, string name, MapTile[,] map) {
            _game = game;
            _tiles = map;
            Name = name;
            _mapWidth = map.GetLength(0);
            _mapHeight = map.GetLength(1);
        }

        public string Name { get; set; }
        public int MaxPower { get; set; }
        public int Sequences { get; set; }
        public StartingPoint StartingPoint { get; set; }
        
        public int Width => _mapWidth;
        public int Height => _mapHeight;

        public void Init()
        {
            
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                _tiles[x, y]?.Draw();
            }
            
            for (var x = -1; x < Width + 1; x++)
            {
                var topPos = _game.Camera.WorldToScreenPos(new Vector2(x * TileSize,-1*TileSize));
                var bottomPos = _game.Camera.WorldToScreenPos(new Vector2(x * TileSize, Height*TileSize));
                
                _border.Add(new Rock(_game) { Position = topPos });
                _border.Add(new Rock(_game) { Position = bottomPos });
            }
            
            for (var y = -1; y < Height + 1; y++)
            {
                var leftPos = _game.Camera.WorldToScreenPos(new Vector2(-1*TileSize, y * TileSize));
                var rightPos = _game.Camera.WorldToScreenPos(new Vector2(Width*TileSize, y * TileSize));
                
                _border.Add(new Rock(_game) { Position = leftPos });
                _border.Add(new Rock(_game) { Position = rightPos });
            }
        }
        
        public void Draw()
        {
            if (_game.DrawStory)
            {
                _game.Console.Draw(3,5, Sprites.Sprites.Story(_game.Playername));
                return;
            }
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                _tiles[x, y]?.Draw();
            }
            foreach (var border in _border) {
                border.Draw();
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

            foreach (var tile in _border)
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
