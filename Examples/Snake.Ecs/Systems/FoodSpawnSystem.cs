using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Snake.Ecs.Components;

namespace Snake.Ecs.Systems;

/// <summary>When no food exists, places one on a random free cell (not on the snake).</summary>
public sealed class FoodSpawnSystem(int width, int height) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Random _random = new();

    /// <summary>Reused every spawn: which cells the snake covers. Index = y * width + x.</summary>
    private readonly bool[] _occupied = new bool[width * height];

    private Query _food;
    private Query _bodies;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _food = World.Query(Query.With<Food>());
        _bodies = World.Query(Query.With<Position>().Without<Food>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (_food.Count > 0)
            return;

        MarkOccupiedCells();
        var free = _occupied.Length - CountOccupied();
        if (free == 0)
            return;

        // Pick the n-th free cell, so the choice is uniform and never retries.
        var pick = _random.Next(free);
        for (var index = 0; index < _occupied.Length; index++)
        {
            if (_occupied[index] || pick-- > 0)
                continue;

            var cell = new Vector2(index % width, index / width);
            Commands.Create(new Position(cell), new Glyph('@', ConsoleColor.White), new Food());
            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void MarkOccupiedCells()
    {
        Array.Clear(_occupied);
        foreach (var chunk in _bodies.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var x = (int)positions[i].Value.X;
                var y = (int)positions[i].Value.Y;
                if (x >= 0 && x < width && y >= 0 && y < height)
                    _occupied[y * width + x] = true;
            }
        }
    }

    private int CountOccupied()
    {
        var count = 0;
        for (var i = 0; i < _occupied.Length; i++)
        {
            if (_occupied[i])
                count++;
        }

        return count;
    }
}
