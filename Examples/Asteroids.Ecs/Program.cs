using Asteroids.Ecs;

// Optional first argument: number of asteroids (default 50, the same as Examples/Asteroids).
var asteroidCount = args.Length > 0 && int.TryParse(args[0], out var count) ? count : 50;

var game = new AsteroidsEcsGame(asteroidCount)
{
    Name = "Asteroids (ECS)",
    EnableLogger = true,
    ShowFps = true
};
game.Initialize();
game.Start();
