using EternalDigger;

// Optional first argument: the mine's seed. The same seed always digs the same mine.
var seed = args.Length > 0 && int.TryParse(args[0], out var value) ? value : System.Environment.TickCount;

var game = new EternalDiggerGame(seed)
{
    Name = "EternalDigger"
};
game.Initialize();
game.Start();
