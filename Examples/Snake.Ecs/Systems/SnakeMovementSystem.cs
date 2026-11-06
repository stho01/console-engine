using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Snake.Ecs.Components;

namespace Snake.Ecs.Systems;

/// <summary>
/// Every 150 ms the head moves one cell. A new body segment fills the cell it left, and the tail segment is removed,
/// unless the head landed on food: then the food is eaten and the tail stays, so the snake grows by one.
/// </summary>
public sealed class SnakeMovementSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float StepSeconds = 0.15f;

    private Query _heads;
    private Query _segments;
    private Query _food;
    private float _elapsed;
    private long _nextOrder;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _heads = World.Query(Query.With<SnakeHead, Position>());
        _segments = World.Query(Query.With<SnakeSegment>());
        _food = World.Query(Query.With<Food, Position>());
        _nextOrder = FindNewestOrder() + 1;
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        _elapsed += deltaSeconds;
        if (_elapsed < StepSeconds)
            return;

        // Reset rather than subtract, like the original's `_time = 0f`.
        _elapsed = 0f;
        foreach (var chunk in _heads.GetChunks())
        {
            var heads = chunk.GetSpan<SnakeHead>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                Step(heads[i].Heading, ref positions[i].Value);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Step(Direction heading, ref Vector2 headPosition)
    {
        var previous = headPosition;
        headPosition += Offset(heading);
        Commands.Create(new Position(previous), new Glyph('█', ConsoleColor.White), new SnakeSegment(_nextOrder++));

        if (TryFindFoodAt(headPosition, out var food))
            Commands.Destroy(food);
        else
            Commands.Destroy(FindTail());
    }

    private bool TryFindFoodAt(Vector2 cell, out Entity food)
    {
        foreach (var chunk in _food.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (positions[i].Value == cell)
                {
                    food = chunk.Entities[i];
                    return true;
                }
            }
        }

        food = Entity.Null;
        return false;
    }

    /// <summary>The oldest segment. Scanning every segment is fine: it happens once per step, not per frame.</summary>
    private Entity FindTail()
    {
        var tail = Entity.Null;
        var oldest = long.MaxValue;
        foreach (var chunk in _segments.GetChunks())
        {
            var segments = chunk.GetSpan<SnakeSegment>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (segments[i].Order < oldest)
                {
                    oldest = segments[i].Order;
                    tail = chunk.Entities[i];
                }
            }
        }

        return tail;
    }

    private long FindNewestOrder()
    {
        var newest = 0L;
        foreach (var chunk in _segments.GetChunks())
        {
            var segments = chunk.GetSpan<SnakeSegment>();
            for (var i = 0; i < chunk.Count; i++)
                newest = Math.Max(newest, segments[i].Order);
        }

        return newest;
    }

    private static Vector2 Offset(Direction heading) => heading switch
    {
        Direction.North => new Vector2(0, -1),
        Direction.East => new Vector2(1, 0),
        Direction.South => new Vector2(0, 1),
        _ => new Vector2(-1, 0)
    };
}
