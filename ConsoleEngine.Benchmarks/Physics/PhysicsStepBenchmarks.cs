using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Physics;
using Microsoft.Xna.Framework;
using EnginePosition = ConsoleEngine.Ecs.Components.Position;
using EngineVelocity = ConsoleEngine.Ecs.Components.Velocity;

namespace ConsoleEngine.Benchmarks.Physics;

/// <summary>
/// One whole <see cref="PhysicsSystem"/> step (integrate, gather, grid broadphase, narrowphase, pair cache, events,
/// solver with 4 substeps) on the same bodies as <see cref="BruteForceCollisionBenchmarks"/>: compare the two tables.
/// The bodies are dynamic circles without gravity that start at random places. Overlapping ones are pushed apart and
/// then rest against each other, so most contacts persist from step to step (warm started), as in a running game.
/// </summary>
[MemoryDiagnoser]
public class PhysicsStepBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int CellsPerBody = 16;
    private const float MinRadius = .5f;
    private const float MaxRadius = 1.5f;
    private const float Step = 1f / 60f;

    private World _world;
    private SystemGroup _group;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _world = new World(Count);
        _group = new SystemGroup(_world);
        _group.Add(new PhysicsSystem(cellSize: 4f));

        // Same random sequence as BruteForceCollisionBenchmarks.
        var random = new Random(42);
        var side = MathF.Sqrt(Count * CellsPerBody);
        for (var i = 0; i < Count; i++)
        {
            var position = new Vector2(random.NextSingle() * side, random.NextSingle() * side);
            var radius = MinRadius + random.NextSingle() * (MaxRadius - MinRadius);
            _world.Create(new EnginePosition(position), new EngineVelocity(Vector2.Zero), RigidBody.Dynamic(),
                Collider.Circle(radius));
        }

        // Grow every buffer before measuring.
        _group.Update(Step);
        _group.Update(Step);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark]
    public void Step60Hz() => _group.Update(Step);
}
