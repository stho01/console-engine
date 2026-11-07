using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Ecs;
using ConsoleEngine.Physics;
using EnginePosition = ConsoleEngine.Ecs.Components.Position;
using EngineVelocity = ConsoleEngine.Ecs.Components.Velocity;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Physics;

/// <summary>
/// One whole <see cref="PhysicsSystem"/> step (integrate, gather, grid broadphase, narrowphase, pair cache, events,
/// solver with 4 substeps) on the same bodies as <see cref="BruteForceCollisionBenchmarks"/>: compare the two tables.
/// The bodies are dynamic circles without gravity that start at random places. Setup lets overlapping ones push apart
/// and come to rest, so the measured steps are the steady state of a running game: resting, warm-started contacts.
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
    private const int SettleSteps = 600;

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

        // Let the overlapping start settle (10 simulated seconds) so every measured step does the same work. Without it,
        // the result depended on how many warm-up steps BenchmarkDotNet happened to run: 2.0 or 3.2 ms for 10 000.
        for (var i = 0; i < SettleSteps; i++)
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
