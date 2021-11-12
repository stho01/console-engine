using System.IO;
using Microsoft.Xna.Framework;
using MyAwesomeConsoleGame.Entities.Tiles;

namespace MyAwesomeConsoleGame
{
    public class WorldLoader
    {
        private readonly MyAwesomeGame _game;

        public WorldLoader(MyAwesomeGame game)
        {
            _game = game;
        }
        
        public World LoadWorld(string path)
        {
            var content = File.ReadAllLines(path);
            
            return Parse(content);
        }

        private World Parse(string[] lines)
        {
            string name = string.Empty;
            int sequences = 0;
            int width = 0;
            int height = 0;
            MapTile[,] tiles = null;
            int y = 0;
            
            foreach (var line in lines)
            {
                if (line.StartsWith("!"))
                {
                    var keyValue = line.Split(":");
                    var key = keyValue[0].ToLower().Trim();
                    var value = keyValue[1].Trim();
                    // property

                    switch (key)
                    {
                        case "!name": name = value; break;
                        case "!sequences": sequences = int.Parse(value); break;
                        case "!width": width = int.Parse(value); break;
                        case "!height": height = int.Parse(value); break;
                    }

                    if (width != 0 && height != 0)
                        tiles = new MapTile[width, height];
                }
                else
                {
                    
                    for (var x = 0; x < line.Length; x++)
                    {
                    
                        var type = line[x];
                        MapTile tile = type switch
                        {
                            'S' => new StartingPoint(_game),
                            'F' => new FinishPoint(_game),
                            'H' => new Rock(_game),
                            'P' => new PlantSpot(_game),
                            'C' => new Craves(_game),
                            'B' => new BonusPoint(_game),
                            _ => null
                        };

                        if (tiles != null && tile != null)
                        {
                            tile.Position = new Vector2(x, y) * 3; 
                            tiles[x, y] = tile;
                        }
                    }
                    y++;
                 
                }
            }

            return new World(_game, name, tiles) {
                Sequences = sequences
            };
        }
    }

}