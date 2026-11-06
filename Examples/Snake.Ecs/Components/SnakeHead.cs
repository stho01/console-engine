namespace Snake.Ecs.Components;

public enum Direction
{
    North = 1,
    East = 2,
    South = 3,
    West = 4
}

/// <summary>The steered end of the snake. The body follows as <see cref="SnakeSegment"/> entities.</summary>
public struct SnakeHead(Direction heading)
{
    public Direction Heading = heading;
}
