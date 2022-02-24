using System;
using Platformer;

var game = new PlatformerGame {
    EnableLogger = true,
    IsDebugMode = true
};
            
game.Initialize();
game.Start();
Console.ReadKey();