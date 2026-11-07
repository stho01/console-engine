using System;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>What <see cref="CharacterMover.MoveAndSlide"/> ran into.</summary>
[Flags]
public enum SlideCollisions : byte
{
    None = 0,

    /// <summary>A surface facing up (normal within 45° of up): the character stands on it.</summary>
    Floor = 1,

    /// <summary>A surface facing down: the character bumped its head.</summary>
    Ceiling = 2,

    /// <summary>Anything steeper.</summary>
    Wall = 4
}

/// <summary>
/// Kinematic character movement, as Godot's <c>CharacterBody2D.move_and_slide</c> and Box2D's character mover: the
/// character is a box that is swept through the world with <see cref="IPhysicsQueries.CastBox"/>, stops at what it
/// hits and slides along it with the rest of its movement. Platformer players feel precise this way, where a dynamic
/// body would feel floaty or jitter. The physics never moves a character: give it no <see cref="RigidBody"/>.
/// </summary>
public static class CharacterMover
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Up for the usual console coordinates, where y grows downwards.</summary>
    public static readonly Vector2 DefaultUp = new(0f, -1f);

    /// <summary>
    /// Gap kept between the character and what it stops at, in cells, so the next move starts outside the surface and
    /// sliding along a floor doesn't keep hitting it.
    /// </summary>
    public const float SkinWidth = .01f;

    /// <summary>Hits handled per move. Corners need two; more only helps in tight gaps.</summary>
    public const int MaxSlides = 4;

    /// <summary>cos 45°: a surface at most this far from up is a floor.</summary>
    private const float FloorMinDot = .7071f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Moves the box at <paramref name="center"/> by <c>velocity * deltaSeconds</c>, stopping at colliders and solid
    /// tiles in <paramref name="mask"/> and sliding along them. The velocity loses the part that went into each surface
    /// it hit, so landing zeroes the vertical speed and a wall the horizontal one.
    /// </summary>
    /// <param name="ignore">The character's own entity, if it has a collider.</param>
    public static SlideCollisions MoveAndSlide(IPhysicsQueries physics, ref Vector2 center, ref Vector2 velocity,
        Vector2 halfExtents, float deltaSeconds, uint mask = Collider.AllCategories, Entity ignore = default) =>
        MoveAndSlide(physics, ref center, ref velocity, halfExtents, deltaSeconds, DefaultUp, mask, ignore);

    /// <inheritdoc cref="MoveAndSlide(IPhysicsQueries, ref Vector2, ref Vector2, Vector2, float, uint, Entity)"/>
    /// <param name="up">Unit vector pointing up, to tell floors from ceilings and walls.</param>
    public static SlideCollisions MoveAndSlide(IPhysicsQueries physics, ref Vector2 center, ref Vector2 velocity,
        Vector2 halfExtents, float deltaSeconds, Vector2 up, uint mask = Collider.AllCategories, Entity ignore = default)
    {
        ArgumentNullException.ThrowIfNull(physics);
        var collisions = SlideCollisions.None;
        var motion = velocity * deltaSeconds;

        for (var slide = 0; slide < MaxSlides && motion.LengthSquared() > 1e-12f; slide++)
        {
            if (!physics.CastBox(center, halfExtents, motion, mask, ignore, out var hit))
            {
                center += motion;
                break;
            }

            var normal = hit.Normal;
            center = hit.Point + normal * SkinWidth;
            collisions |= Classify(normal, up);

            // Slide: keep only the part of the rest of the move (and of the velocity) that runs along the surface.
            var rest = motion * (1f - hit.Fraction);
            motion = rest - normal * MathF.Min(Vector2.Dot(rest, normal), 0f);
            velocity -= normal * MathF.Min(Vector2.Dot(velocity, normal), 0f);
        }

        return collisions;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static SlideCollisions Classify(Vector2 normal, Vector2 up)
    {
        var upness = Vector2.Dot(normal, up);
        if (upness >= FloorMinDot)
            return SlideCollisions.Floor;

        return upness <= -FloorMinDot ? SlideCollisions.Ceiling : SlideCollisions.Wall;
    }
}
