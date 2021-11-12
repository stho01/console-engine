using System;
using System.Linq;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace MyAwesomeConsoleGame
{
    public class World
    {
        private readonly MyAwesomeGame _game;
        
        
        private static readonly Sprite StartSprite = Sprite.FromStringArray(new []{
            "S▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.Green);
        private static readonly Sprite FinishSprite = Sprite.FromStringArray(new []{
            "F▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.DarkGreen);
        private static readonly Sprite RockSprite = Sprite.FromStringArray(new []{
            "H▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.DarkGray);
        private static readonly Sprite CravesSprite = Sprite.FromStringArray(new []{
            "C▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.DarkYellow);
        private static readonly Sprite PlantSpotSprite = Sprite.FromStringArray(new []{
            "P▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.Magenta);
        private static readonly Sprite BonusSpotSprite = Sprite.FromStringArray(new []{
            "B▓▒",
            "▓▒▓",
            "▒▓▒",
        }, ConsoleColor.White);

        private readonly int _mapWidth;
        private readonly int _mapHeight;
        public const int TileSize = 3;
        
        public World(MyAwesomeGame game, string name, string[] map) {
            _game = game;
            Map = map;
            _mapWidth = map.Max(row => row.Length);
            _mapHeight = map.Length;
        }

        public string Name { get; set; }
        public int Sequences { get; set; }
        
        public int Width => _mapWidth;
        public int Height => _mapHeight;
        public string[] Map { get; }
        
        public void Draw()
        {
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                var pos = _game.Camera.WorldToScreenPos(
                    new Vector2(x * TileSize, y * TileSize));
                
                var tile = Map[y][x];

                var sprite = tile switch
                {
                    'S' => StartSprite,
                    'F' => FinishSprite,
                    'H' => RockSprite,
                    'P' => PlantSpotSprite,
                    'C' => CravesSprite,
                    'B' => BonusSpotSprite,
                    _ => null
                };
                
                if (sprite != null)
                {
                    var posX = pos.X;
                    var posY = pos.Y;
                    _game.Console.Draw(posX, posY, sprite);
                }
            }
            
            // Draw boarders
            
            // top and bottom
            // for (var x = -1; x < Height + 1; x++)
            // {
            //     var topPos = _game.Camera.WorldToScreenPos(new Vector2(x * TileSize,-1*TileSize));
            //     var bottomPos = _game.Camera.WorldToScreenPos(new Vector2(x * TileSize, Height+1*TileSize));
            //     
            //     _game.Console.Draw(topPos.X, topPos.Y, RockSprite);
            //     _game.Console.Draw(bottomPos.X, bottomPos.Y, RockSprite);
            // }
            
        }

        public char GetTile(float x, float y) => GetTileFromMapCoords((int)(x/TileSize), (int)(y/TileSize));
        public char GetTile(int x, int y) => GetTileFromMapCoords(x/TileSize, y/TileSize);

        private char GetTileFromMapCoords(int mapX, int mapY)
        {
            if (mapX >= 0 && mapX < Width 
             && mapY >= 0 && mapY < Height)
            {
                return Map[mapY][mapX];
            }

            return ' ';
        }
    }
}
