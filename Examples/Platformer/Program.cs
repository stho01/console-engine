using System;
using Platformer;

var game = new PlatformerGame {
    IsDebugMode = true
};
            
game.Initialize();
game.Start();
Console.ReadKey();