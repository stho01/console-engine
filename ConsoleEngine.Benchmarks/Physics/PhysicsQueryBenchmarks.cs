using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Ecs;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using EnginePosition = ConsoleEngine.Ecs.Components.Position;

namespace ConsoleEngine.Benchmarks.Physics;

/// <summary>
/// 1 000 spatial queries per operation among <see cref="Count"/> static circles, at the density of
/// <see cref="PhysicsStepBenchmarks"/>: raycasts 20 cells long and box casts (1×1 box) 2 cells long, the length of a
/// fast character's move in one step. The first query after the step rebuilds the query grid; that is measured
/// separately in <see cref="FirstQueryAfterStep"/>.
/// </summary>
[MemoryDiagnoser]
public class PhysicsQueryBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int CellsPerBody = 16;
    private const int QueryCount = 1_000;

    private World _world;
    private SystemGroup _group;
    private PhysicsSystem _physics;
    private Vector2[] _origins;
    private Vector2[] _directions;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(1_000, 10_000)]
    public int Count { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _world = new World(Count);
        _group = new SystemGroup(_world);
        _physics = _group.Add(new PhysicsSystem(cellSize: 4f));

        var random = new Random(42);
        var side = MathF.Sqrt(Count * CellsPerBody);
        for (var i = 0; i < Count; i++)
        {
            var position = new Vector2(random.NextSingle() * side, random.NextSingle() * side);
            _world.Create(new EnginePosition(position), Collider.Circle(.5f + random.NextSingle()));
        }

        _origins = new Vector2[QueryCount];
        _directions = new Vector2[QueryCount];
        for (var i = 0; i < QueryCount; i++)
        {
            _origins[i] = new Vector2(random.NextSingle() * side, random.NextSingle() * side);
            var angle = random.NextSingle() * MathF.Tau;
            _directions[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        _group.Update(1f / 60f);
        _physics.Raycast(Vector2.Zero, Vector2.One, Collider.AllCategories, out _);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark]
    public int Raycasts()
    {
        var hits = 0;
        for (var i = 0; i < QueryCount; i++)
        {
            if (_physics.Raycast(_origins[i], _directions[i] * 20f, Collider.AllCategories, out _))
                hits++;
        }

        return hits;
    }

    [Benchmark]
    public int BoxCasts()
    {
        var hits = 0;
        var half = new Vector2(.5f, .5f);
        for (var i = 0; i < QueryCount; i++)
        {
            if (_physics.CastBox(_origins[i], half, _directions[i] * 2f, Collider.AllCategories, Entity.Null, out _))
                hits++;
        }

        return hits;
    }

    /// <summary>A step plus one query: what a game that queries pays for the query grid each step.</summary>
    [Benchmark]
    public bool FirstQueryAfterStep()
    {
        _group.Update(1f / 60f);
        return _physics.Raycast(_origins[0], _directions[0] * 20f, Collider.AllCategories, out _);
    }
}
