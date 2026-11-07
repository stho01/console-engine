using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using PhysicsShowcase.Components;

namespace PhysicsShowcase;

/// <summary>
/// Creates the bodies the player drops. Each shape is registered once in the physics' <see cref="PolygonLibrary"/> and,
/// with the same corners, in the <see cref="OutlineLibrary"/> that draws it. A body has more components than
/// <c>Create</c> takes, so the rest are added to the same deferred entity.
/// </summary>
public sealed class BodyFactory
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float MaxSideSpeed = 10f;

    private readonly Random _random;
    private readonly int _boxOutline;
    private readonly int _triangle;
    private readonly int _triangleOutline;
    private readonly int _pentagon;
    private readonly int _pentagonOutline;

    //**********************************************************
    //** ctor
    //**********************************************************

    public BodyFactory(PhysicsSystem physics, OutlineLibrary outlines, Random random)
    {
        _random = random;
        _boxOutline = outlines.Add([new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1)]);

        // Corners around the origin (the point bodies turn around), so they spin naturally.
        _triangle = physics.Polygons.Add([new Vector2(0, -1.6f), new Vector2(1.5f, 1f), new Vector2(-1.5f, 1f)]);
        _triangleOutline = outlines.Add(physics.Polygons.GetVertices(_triangle));
        _pentagon = physics.Polygons.Add(RegularPolygon(5, 1.7f));
        _pentagonOutline = outlines.Add(physics.Polygons.GetVertices(_pentagon));
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>A bouncy ball (restitution 0.7).</summary>
    public void CreateBall(CommandBuffer commands, Vector2 position)
    {
        var ball = commands.Create(
            new Position(position),
            new Velocity(RandomSideways()),
            RigidBody.Dynamic(),
            Collider.Circle(.5f) with
            {
                Restitution = .7f,
                Category = Layers.Solid,
                Mask = Layers.SolidMask
            },
            new Glyph('●', ConsoleColor.Yellow),
            new Dropped());
        AddCommon(commands, ball, ConsoleColor.Yellow);
    }

    /// <summary>A ball that passes through solid bodies but not through tiles or other ghosts.</summary>
    public void CreateGhost(CommandBuffer commands, Vector2 position)
    {
        var ghost = commands.Create(
            new Position(position),
            new Velocity(RandomSideways()),
            RigidBody.Dynamic(.5f),
            Collider.Circle(.5f) with
            {
                Restitution = .4f,
                Category = Layers.Ghost,
                Mask = Layers.GhostMask
            },
            new Glyph('○', ConsoleColor.Magenta),
            new Dropped());
        AddCommon(commands, ghost, ConsoleColor.Magenta);
    }

    /// <summary>A 2×2 box that turns.</summary>
    public void CreateBox(CommandBuffer commands, Vector2 position) =>
        CreateTurning(commands, position, Collider.Box(Vector2.One), mass: 2f,
            new Outline(_boxOutline, '#', ConsoleColor.Cyan), ConsoleColor.Cyan);

    public void CreateTriangle(CommandBuffer commands, Vector2 position) =>
        CreateTurning(commands, position, Collider.Polygon(_triangle), mass: 1.5f,
            new Outline(_triangleOutline, '▲', ConsoleColor.Green), ConsoleColor.Green);

    public void CreatePentagon(CommandBuffer commands, Vector2 position) =>
        CreateTurning(commands, position, Collider.Polygon(_pentagon), mass: 2.5f,
            new Outline(_pentagonOutline, '*', ConsoleColor.Red), ConsoleColor.Red);

    /// <summary>One of the five kinds, picked at random.</summary>
    public void CreateRandom(CommandBuffer commands, Vector2 position)
    {
        switch (_random.Next(5))
        {
            case 0: CreateBall(commands, position); break;
            case 1: CreateGhost(commands, position); break;
            case 2: CreateBox(commands, position); break;
            case 3: CreateTriangle(commands, position); break;
            default: CreatePentagon(commands, position); break;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void CreateTurning(CommandBuffer commands, Vector2 position, Collider collider, float mass,
        Outline outline, ConsoleColor color)
    {
        var body = commands.Create(
            new Position(position),
            new Velocity(RandomSideways()),
            new Rotation(_random.NextSingle() * MathF.Tau),
            new AngularVelocity((_random.NextSingle() * 2f - 1f) * 3f),
            RigidBody.Dynamic(mass),
            collider with { Category = Layers.Solid, Mask = Layers.SolidMask });
        commands.Add(body, outline);
        commands.Add(body, new Dropped());
        AddCommon(commands, body, color);
    }

    /// <summary>Sleep colouring and smooth drawing between fixed steps.</summary>
    private static void AddCommon(CommandBuffer commands, Entity body, ConsoleColor color)
    {
        commands.Add(body, new Tint(color));
        commands.Add(body, new Interpolation());
        commands.Add(body, new RenderOffset());
    }

    private Vector2 RandomSideways() => new((_random.NextSingle() * 2f - 1f) * MaxSideSpeed, 0f);

    private static Vector2[] RegularPolygon(int sides, float radius)
    {
        var points = new Vector2[sides];
        for (var i = 0; i < sides; i++)
        {
            var angle = MathF.Tau * i / sides - MathF.PI / 2f;
            points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        return points;
    }
}
