using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Physics;
using Microsoft.Xna.Framework;
using PhysicsSandbox.Components;

namespace PhysicsSandbox;

/// <summary>Creates the dropped bodies: bouncy balls and heavier boxes, one cell each.</summary>
public sealed class BodySpawner(Random random)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float BallRestitution = .6f;
    private const float BoxMass = 3f;
    private const float MaxSideSpeed = 8f;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void CreateBall(CommandBuffer commands, Vector2 position) =>
        commands.Create(
            new Position(position),
            new Velocity(RandomSideways()),
            RigidBody.Dynamic(),
            Collider.Circle(.5f) with { Restitution = BallRestitution },
            new Glyph('●', ConsoleColor.Yellow),
            new Dropped());

    public void CreateBox(CommandBuffer commands, Vector2 position) =>
        commands.Create(
            new Position(position),
            new Velocity(RandomSideways()),
            RigidBody.Dynamic(BoxMass),
            Collider.Box(new Vector2(.5f, .5f)),
            new Glyph('■', ConsoleColor.Cyan),
            new Dropped());

    //**********************************************************
    //** internals
    //**********************************************************

    private Vector2 RandomSideways() => new((random.NextSingle() * 2f - 1f) * MaxSideSpeed, 0f);
}
