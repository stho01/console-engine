using Swarm;

// Optional first argument: number of particles at start (default 100 000).
var particleCount = args.Length > 0 && int.TryParse(args[0], out var count) ? count : 100_000;

var game = new SwarmGame(particleCount)
{
    Name = "Swarm (ECS)",
    EnableLogger = true,
    ShowFps = true
};
game.Initialize();
game.Start();
