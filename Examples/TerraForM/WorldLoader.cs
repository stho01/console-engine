using System.IO;
using Microsoft.Xna.Framework;
using TerraForM.GameObjects.Tiles;

namespace TerraForM;

public class WorldLoader
{
    public static World LoadWorld(TerraformGame game, string path)
    {
        var content = File.ReadAllLines(path);
            
        return Parse(game, content);
    }

    private static World Parse(TerraformGame game, string[] lines)
    {
        string name = string.Empty;
        int sequences = 0;
        int width = 0;
        int height = 0;
        int maxPower = 0;
        MapTile[,] tiles = null;
        int y = 0;
        StartingPoint startingPoint = null;
            
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
                    case "!power": maxPower = int.Parse(value); break;
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
                        'S' => new StartingPoint(game),
                        'F' => new FinishPoint(game),
                        'H' => new Rock(game),
                        'P' => new PlantSpot(game),
                        'C' => new Craves(game),
                        'B' => new BonusPoint(game),
                        _ => null
                    };

                    if (tile is StartingPoint sp)
                        startingPoint = sp;

                    if (tiles != null && tile != null)
                    {
                        tile.Position = new Vector2(x, y) * World.TileSize; 
                        tiles[x, y] = tile;
                    }
                }
                y++;
                 
            }
        }

            
        var world = new World(game, name, tiles) {
            Sequences = sequences,
            StartingPoint = startingPoint,
            MaxPower = maxPower
        };
        world.Init();
        return world;
    }
}