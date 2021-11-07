using System;
using Platformer;

var game = new PlatformerGame {
    EnableLogger = true
};
            
game.Initialize();
game.Start();
Console.ReadKey();