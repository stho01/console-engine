using Platformer.Ecs;

var game = new PlatformerEcsGame
{
    Name = "Platformer (ECS)",
    ShowFps = true,
    IsDebugMode = true
};
game.Initialize();
game.Start();
