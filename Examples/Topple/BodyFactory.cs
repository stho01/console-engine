using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using Topple.Components;
using Topple.Levels;
using Topple.Rendering;

namespace Topple;

/// <summary>
/// Creates every body of the game. Level bodies are created straight in the <see cref="World"/> when the scene loads;
/// shots and debris during play through a <see cref="CommandBuffer"/>. Shapes are registered once: the roof in the
/// physics' <see cref="PolygonLibrary"/>, and every drawn shape in the <see cref="ShapeLibrary"/>.
/// </summary>
public sealed class BodyFactory
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float StoneRadius = .9f;
    public const float BombRadius = .8f;
    private const float StoneMass = 5f;
    private const float BombMass = 3f;
    private const float DebrisRadius = .2f;

    /// <summary>A triangle 6 wide and 3 high around its centroid.</summary>
    private static readonly Vector2[] RoofCorners = [new(0f, -2f), new(3f, 1f), new(-3f, 1f)];

    private const float RoofArea = 9f;

    private readonly ShapeLibrary _shapes;
    private readonly int _roof;
    private readonly int _roofShape;

    //**********************************************************
    //** ctor
    //**********************************************************

    public BodyFactory(PhysicsSystem physics, ShapeLibrary shapes)
    {
        _shapes = shapes;
        _roof = physics.Polygons.Add(RoofCorners);
        _roofShape = shapes.Add(new Shape(RoofCorners, isRoof: true));
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    public Entity CreatePiece(World world, in Piece piece)
    {
        ref readonly var info = ref Materials.Get(piece.Material);
        var isRoof = piece.Shape == PieceShape.Roof;
        var collider = isRoof ? Collider.Polygon(_roof) : Collider.Box(piece.HalfSize);
        var area = isRoof ? RoofArea : 4f * piece.HalfSize.X * piece.HalfSize.Y;

        var entity = world.Create(
            new Position(piece.Center),
            new Velocity(Vector2.Zero),
            new Rotation(0f),
            new AngularVelocity(0f),
            RigidBody.Dynamic(info.Density * area),
            collider with { Category = Layers.Blocks, Mask = Layers.BlockMask, Friction = info.Friction });
        world.Add(entity, new Block(piece.Material, info.Health));
        world.Add(entity, new LastVelocity());
        world.Add(entity, new Interpolation());
        world.Add(entity, new RenderOffset());

        if (piece.Shape == PieceShape.Crystal)
            world.Add(entity, new Crystal());
        else
            world.Add(entity, new BlockLook(isRoof ? _roofShape : _shapes.AddBox(piece.HalfSize), piece.Material));

        return entity;
    }

    /// <summary>A kinematic wooden raft: the physics moves it by its velocity, which <c>FerrySystem</c> sets.</summary>
    public void CreateFerry(World world, FerryPlan plan) =>
        world.Create(
            new Position(plan.Anchor),
            new Velocity(Vector2.Zero),
            RigidBody.Kinematic(),
            Collider.Box(plan.HalfSize) with { Category = Layers.Blocks, Friction = .8f },
            new BlockLook(_shapes.AddBox(plan.HalfSize), Material.Wood),
            new Ferry(plan.Anchor, plan.Amplitude, plan.PeriodSeconds));

    /// <summary>A static sensor over the water part of a pit.</summary>
    public static void CreateWater(World world, TileArea water) =>
        world.Create(
            new Position(new Vector2((water.Left + water.Right + 1) / 2f, (water.Top + water.Bottom + 1) / 2f)),
            Collider.Box(new Vector2((water.Right - water.Left + 1) / 2f, (water.Bottom - water.Top + 1) / 2f)) with
            {
                IsSensor = true,
                Category = Layers.Water,
                Mask = Layers.WaterMask
            },
            new Water());

    public static void CreateShot(CommandBuffer commands, ShotKind kind, Vector2 position, Vector2 velocity)
    {
        var stone = kind == ShotKind.Stone;
        var shot = commands.Create(
            new Position(position),
            new Velocity(velocity),
            RigidBody.Dynamic(stone ? StoneMass : BombMass),
            Collider.Circle(stone ? StoneRadius : BombRadius) with
            {
                Category = Layers.Shots,
                Mask = Layers.ShotMask,
                Restitution = stone ? .2f : .1f
            },
            stone ? new Glyph('●', ConsoleColor.Gray) : new Glyph('☼', ConsoleColor.Red),
            new Shot(kind));
        commands.Add(shot, new LastVelocity { Value = velocity });
        commands.Add(shot, new Interpolation());
        commands.Add(shot, new RenderOffset());
    }

    /// <summary>A splinter or spark that only collides with the ground.</summary>
    public static void CreateDebris(CommandBuffer commands, Vector2 position, Vector2 velocity, char character,
        ConsoleColor color, float seconds) =>
        commands.Create(
            new Position(position),
            new Velocity(velocity),
            RigidBody.Dynamic(.1f),
            Collider.Circle(DebrisRadius) with { Category = Layers.Debris, Mask = Layers.DebrisMask, Restitution = .4f },
            new Glyph(character, color),
            new Debris(seconds));
}
