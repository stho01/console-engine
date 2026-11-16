using Doom;

var game = new DoomGame(ResolutionPrompt.Choose(args))
{
    Name = "Doom (console)"
};
game.Initialize();
game.Start();
