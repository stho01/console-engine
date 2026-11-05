using ConsoleGame.Ecs;

var game = new ConsoleEcsGame
{
    ShowFps = true,
    EnableLogger = true
};

game.Initialize();
game.Start();
