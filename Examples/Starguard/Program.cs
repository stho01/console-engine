using Starguard;

var game = new StarguardGame
{
    Name = "Starguard",
    EnableLogger = true,
    ShowFps = true
};
game.Initialize();
game.Start();
