using System;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Visits the cells of a uniform grid along a ray in order (Amanatides and Woo, "A Fast Voxel Traversal Algorithm for
/// Ray Tracing", 1987). Start at the origin's cell, then call <see cref="Next"/> until it returns false (the ray ends,
/// fraction 1).
/// </summary>
internal struct GridWalker
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly int _stepX;
    private readonly int _stepY;
    private readonly float _deltaX;
    private readonly float _deltaY;
    private float _nextX;
    private float _nextY;

    //**********************************************************
    //** ctor
    //**********************************************************

    public GridWalker(Vector2 origin, Vector2 translation, Vector2 cellSize, Vector2 gridOrigin)
    {
        var local = origin - gridOrigin;
        X = (int)MathF.Floor(local.X * (1f / cellSize.X));
        Y = (int)MathF.Floor(local.Y * (1f / cellSize.Y));
        Init(local.X, translation.X, cellSize.X, X, out _stepX, out _nextX, out _deltaX);
        Init(local.Y, translation.Y, cellSize.Y, Y, out _stepY, out _nextY, out _deltaY);
        Enter = 0f;
        EnterNormal = Vector2.Zero;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int X { get; private set; }
    public int Y { get; private set; }

    /// <summary>Ray fraction where the current cell was entered. 0 for the start cell.</summary>
    public float Enter { get; private set; }

    /// <summary>Normal of the cell face the ray crossed to enter the current cell. Zero for the start cell.</summary>
    public Vector2 EnterNormal { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Steps into the next cell. False when the ray ends before reaching it.</summary>
    public bool Next()
    {
        if (_nextX < _nextY)
        {
            if (_nextX > 1f)
                return false;

            X += _stepX;
            Enter = _nextX;
            _nextX += _deltaX;
            EnterNormal = new Vector2(-_stepX, 0f);
            return true;
        }

        if (_nextY > 1f)
            return false;

        Y += _stepY;
        Enter = _nextY;
        _nextY += _deltaY;
        EnterNormal = new Vector2(0f, -_stepY);
        return true;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Init(float local, float direction, float cellSize, int cell, out int step, out float next,
        out float delta)
    {
        if (direction > 0f)
        {
            step = 1;
            next = ((cell + 1) * cellSize - local) / direction;
            delta = cellSize / direction;
        }
        else if (direction < 0f)
        {
            step = -1;
            next = (cell * cellSize - local) / direction;
            delta = -cellSize / direction;
        }
        else
        {
            step = 0;
            next = float.PositiveInfinity;
            delta = float.PositiveInfinity;
        }
    }
}
