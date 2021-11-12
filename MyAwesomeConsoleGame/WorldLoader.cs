using System.Collections.Generic;
using System.IO;

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
            var content = new List<string>();
            
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
                        case "!name": name = value;
                            break;
                        case "!sequences": sequences = int.Parse(value);
                            break;
                    }
                }
                else
                {
                    content.Add(line);
                }
            }

            return new World(_game, name, content.ToArray()) {
                Sequences = sequences
            };
        }
    }

}