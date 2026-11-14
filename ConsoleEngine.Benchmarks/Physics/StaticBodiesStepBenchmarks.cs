using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Ecs;
using ConsoleEngine.Physics;
using EnginePosition = ConsoleEngine.Ecs.Components.Position;
using EngineVelocity = ConsoleEngine.Ecs.Components.Velocity;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Physics;

/// <summary>
/// A <see cref="PhysicsSystem"/> step in a level made mostly of static bodies, as a race track's walls or a pool
/// table's cushions: a few dynamic balls rolling among <see cref="StaticCount"/> static boxes laid out in a grid, so
/// the static bodies' share of the broadphase shows.
/// </summary>
[MemoryDiagnoser]
public class StaticBodiesStepBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int DynamicCount = 16;
    private const float Spacing = 6f;
    private const float Step = 1f / 120f;
    private const int SettleSteps = 240;

    private World _world;
    private SystemGroup _group;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(30, 150, 1_000)]
    public int StaticCount { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        _group = new SystemGroup(_world);
        _group.Add(new PhysicsSystem(cellSize: 4f) { EnableSleep = false });

        var columns = (int)MathF.Ceiling(MathF.Sqrt(StaticCount));
        for (var i = 0; i < StaticCount; i++)
        {
            var position = new Vector2(i % columns * Spacing, i / columns * Spacing);
            _world.Create(new EnginePosition(position), RigidBody.Static(), Collider.Box(new Vector2(2f, .5f)));
        }

        // Four long walls around the grid (more static bodies, each in many cells) keep the balls in.
        var side = columns * Spacing;
        var middle = new Vector2(side * .5f - Spacing * .5f);
        var half = side * .5f + 1f;
        AddWall(middle + new Vector2(0f, -half), new Vector2(half, .5f));
        AddWall(middle + new Vector2(0f, half), new Vector2(half, .5f));
        AddWall(middle + new Vector2(-half, 0f), new Vector2(.5f, half));
        AddWall(middle + new Vector2(half, 0f), new Vector2(.5f, half));

        // Balls that keep moving (no damping, elastic), between the boxes.
        var random = new Random(42);
        for (var i = 0; i < DynamicCount; i++)
        {
            var position = new Vector2(random.NextSingle() * side, random.NextSingle() * side);
            var velocity = new Vector2(random.NextSingle() * 8f - 4f, random.NextSingle() * 8f - 4f);
            _world.Create(new EnginePosition(position), new EngineVelocity(velocity),
                RigidBody.Dynamic(gravityScale: 0f), Collider.Circle(.5f) with { Restitution = 1f });
        }

        for (var i = 0; i < SettleSteps; i++)
            _group.Update(Step);
    }

    private void AddWall(Vector2 center, Vector2 halfExtents) =>
        _world.Create(new EnginePosition(center), RigidBody.Static(), Collider.Box(halfExtents));

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark]
    public void Step120Hz() => _group.Update(Step);
}
