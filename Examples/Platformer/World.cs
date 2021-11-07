using System;
using System.Linq;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace Platformer
{
    public class World
    {
        private readonly PlatformerGame _game;
        private static readonly string[] TileMap = { // TODO: Let different chars be different tile types. coins, consumables, destructible blocks etc. 
            "#.............................................................................................#",
            "#.............................................................................................#",
            "#.............................................................................................#",
            "#.............................................................................................#",
            "#.............................................................................................#",
            "#.............................................................................................#",
            "#................#............................................................................#",
            "#................#............................................................................#",
            "#................#............................................................................#",
            "#................#............................................................................#",
            "#................#............................................................................#",
            "###########......#..........................########.################..........################",
            "#.........#......#...............###################.#.....................######..............",
            "#...........................##################.......#...............########..................",
            "##############################################.#######.........#######.........................",
            ".............................................#............#########............................",
            ".............................................#####################.............................",
        };
        private static readonly Sprite TileSprite = Sprite.FromStringArray(new []{
            "▒▓▒▓",
            "▓▒▓▒",
            "▒▓▒▓",
            "▓▒▓▒",
        }, ConsoleColor.Gray);

        private readonly int _mapWidth;
        private readonly int _mapHeight;
        
        public World(PlatformerGame game) {
            _game = game;
            _mapWidth = TileMap.Max(row => row.Length);
            _mapHeight = TileMap.Length;
        }

        public int Width => _mapWidth;
        public int Height => _mapHeight;
        
        public void Draw()
        {
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                var pos = _game.Camera.WorldToScreenPos(
                    new Vector2(x * TileSprite.Width, y * TileSprite.Height));
                
                var tile = TileMap[y][x];
                if (tile != '.')
                {
                    var posX = (int)pos.X;
                    var posY = (int)pos.Y;
                    
                    _game.Console.Draw(posX, posY, TileSprite);

                    if (_game.IsDebugMode)
                    {
                        _game.Console.Draw(posX, posY, x.ToString(), ConsoleColor.Black, ConsoleColor.White);
                        _game.Console.Draw(posX, posY+1, y.ToString(), ConsoleColor.Black, ConsoleColor.White);    
                    }
                }
            }
        }

        public char GetTile(float x, float y) => GetTileFromMapCoords((int)(x/TileSprite.Width), (int)(y/TileSprite.Height));
        public char GetTile(int x, int y) => GetTileFromMapCoords(x / TileSprite.Width, y / TileSprite.Height);

        private char GetTileFromMapCoords(int mapX, int mapY)
        {
            if (mapX >= 0 && mapX < Width 
             && mapY >= 0 && mapY < Height)
            {
                return TileMap[mapY][mapX];
            }

            return ' ';
        }
    }
}