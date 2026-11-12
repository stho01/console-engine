using System;
using Microsoft.Xna.Framework;
using SkyStack.Components;

namespace SkyStack.Climbing;

/// <summary>
/// Running, jumping, wall bounces and landing for a <see cref="Climber"/>. Horizontal numbers are in columns, vertical
/// ones in rows (a row is twice as tall as a column is wide). The faster the climber runs, the higher it jumps
/// (<see cref="JumpSpeed"/>). Platforms are one-way: they only catch a falling climber.
/// </summary>
public static class ClimbPhysics
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int BodyWidth = 2;
    public const int BodyHeight = 2;

    /// <summary>Returned by <see cref="Step"/> when the climber didn't land.</summary>
    public const int NoLanding = -1;

    /// <summary>Rows/s².</summary>
    public const float Gravity = 120f;

    /// <summary>Upward speed of a jump from standing still, rows/s. Enough to reach the next floor.</summary>
    public const float BaseJumpSpeed = 34f;

    /// <summary>Extra jump speed (rows/s) per column/s of running speed.</summary>
    public const float RunJumpBonus = .75f;

    /// <summary>Columns/s.</summary>
    public const float MaxRunSpeed = 60f;

    /// <summary>Rows/s.</summary>
    public const float MaxFallSpeed = 80f;

    /// <summary>Columns/s² while a run key is held on a floor.</summary>
    public const float GroundAcceleration = 90f;

    /// <summary>Columns/s² while a run key is held in the air.</summary>
    public const float AirAcceleration = 60f;

    /// <summary>Columns/s² of slowing down on a floor without a run key.</summary>
    public const float GroundFriction = 120f;

    /// <summary>Share of the running speed kept when bouncing off a wall.</summary>
    public const float WallBounce = .9f;

    /// <summary>How long the spark of a wall bounce shows.</summary>
    public const float SparkSeconds = .15f;

    /// <summary>Leftmost x of the body.</summary>
    public const float MinX = Layout.InnerLeft;

    /// <summary>Rightmost x of the body.</summary>
    public const float MaxX = Layout.InnerRight - BodyWidth;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static float JumpSpeed(float runSpeed) => BaseJumpSpeed + RunJumpBonus * MathF.Abs(runSpeed);

    /// <summary>
    /// One fixed step: run, jump (while <see cref="Climber.JumpHeld"/> and on a floor), bounce off the walls, fall
    /// and land. Returns the floor landed on, or <see cref="NoLanding"/>.
    /// </summary>
    public static int Step(ref Climber climber, ref Vector2 position, ref Vector2 velocity, Tower tower,
        float deltaSeconds)
    {
        climber.SparkSeconds = MathF.Max(0f, climber.SparkSeconds - deltaSeconds);

        Run(ref climber, ref velocity, deltaSeconds);

        if (climber.IsGrounded && climber.JumpHeld)
        {
            velocity.Y = -JumpSpeed(velocity.X);
            climber.IsGrounded = false;
        }

        position.X += velocity.X * deltaSeconds;
        BounceOffWalls(ref climber, ref position, ref velocity);

        if (climber.IsGrounded && !tower.GetFloor(climber.Floor).Supports(position.X, BodyWidth))
            climber.IsGrounded = false;

        if (climber.IsGrounded)
        {
            velocity.Y = 0f;
            return NoLanding;
        }

        return Fall(ref climber, ref position, ref velocity, tower, deltaSeconds);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Run(ref Climber climber, ref Vector2 velocity, float deltaSeconds)
    {
        if (climber.RunDirection != 0f)
        {
            var acceleration = climber.IsGrounded ? GroundAcceleration : AirAcceleration;
            velocity.X += climber.RunDirection * acceleration * deltaSeconds;
        }
        else if (climber.IsGrounded)
        {
            var slowdown = GroundFriction * deltaSeconds;
            velocity.X = MathF.Abs(velocity.X) <= slowdown ? 0f : velocity.X - MathF.Sign(velocity.X) * slowdown;
        }

        velocity.X = Math.Clamp(velocity.X, -MaxRunSpeed, MaxRunSpeed);
    }

    private static void BounceOffWalls(ref Climber climber, ref Vector2 position, ref Vector2 velocity)
    {
        if (position.X < MinX)
        {
            position.X = MathF.Min(MaxX, 2 * MinX - position.X);
            velocity.X = MathF.Abs(velocity.X) * WallBounce;
            climber.SparkSeconds = SparkSeconds;
            climber.SparkSide = -1;
        }
        else if (position.X > MaxX)
        {
            position.X = MathF.Max(MinX, 2 * MaxX - position.X);
            velocity.X = -MathF.Abs(velocity.X) * WallBounce;
            climber.SparkSeconds = SparkSeconds;
            climber.SparkSide = 1;
        }
    }

    private static int Fall(ref Climber climber, ref Vector2 position, ref Vector2 velocity, Tower tower,
        float deltaSeconds)
    {
        velocity.Y = MathF.Min(velocity.Y + Gravity * deltaSeconds, MaxFallSpeed);
        var startRow = position.Y;
        position.Y += velocity.Y * deltaSeconds;
        if (velocity.Y <= 0f)
            return NoLanding;

        // Every platform row passed on the way down, from the highest one: the first under the feet catches them.
        for (var floor = Tower.FloorAtOrBelow(startRow); floor >= 0; floor--)
        {
            var row = Tower.RowOf(floor);
            if (row > position.Y)
                break;
            if (row < startRow || !tower.GetFloor(floor).Supports(position.X, BodyWidth))
                continue;

            position.Y = row;
            velocity.Y = 0f;
            climber.IsGrounded = true;
            climber.Floor = floor;
            return floor;
        }

        return NoLanding;
    }
}
