using Snake.Ecs;

var game = new SnakeEcsGame
{
    Name = "Snake (ECS)",
    ShowFps = true
};

game.Initialize();
game.Start();
