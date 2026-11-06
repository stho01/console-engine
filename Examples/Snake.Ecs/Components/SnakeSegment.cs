namespace Snake.Ecs.Components;

/// <summary>
/// One body cell. Each step leaves a new segment where the head was; the segment with the lowest
/// <see cref="Order"/> is the tail and is removed unless the snake just ate.
/// </summary>
public struct SnakeSegment(long order)
{
    public long Order = order;
}
