using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Ecs;

/// <summary>
/// Structural operations and random access on <see cref="World"/>. One invocation touches all <see cref="Count"/>
/// entities. Query iteration comes with phase 2 and is compared to <c>OopUpdateBenchmarks</c> there.
/// </summary>
[MemoryDiagnoser]
public class WorldBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private World _world;
    private Entity[] _entities;
    private Entity[] _shuffled;
    private Entity[] _spawned;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(10_000, 100_000, 1_000_000)]
    public int Count { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _world = new World(Count);
        _entities = new Entity[Count];
        _world.Create(_entities, new Position(), new Velocity { Value = Vector2.One });

        _shuffled = (Entity[])_entities.Clone();
        new Random(42).Shuffle(_shuffled);
        _spawned = new Entity[Count];
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    /// <summary>Spawn and despawn wave with warm pools: what a game does every frame with particles.</summary>
    [Benchmark]
    public void CreateThenDestroy_OneByOne()
    {
        var spawned = _spawned;
        for (var i = 0; i < spawned.Length; i++)
            spawned[i] = _world.Create(new Position(), new Velocity());
        for (var i = 0; i < spawned.Length; i++)
            _world.Destroy(spawned[i]);
    }

    [Benchmark]
    public void CreateThenDestroy_Bulk()
    {
        var spawned = _spawned;
        _world.Create(spawned, new Position(), new Velocity());
        for (var i = 0; i < spawned.Length; i++)
            _world.Destroy(spawned[i]);
    }

    [Benchmark(Baseline = true)]
    public float Get_InCreationOrder() => SumX(_entities);

    /// <summary>Random access through handles, e.g. following references between entities.</summary>
    [Benchmark]
    public float Get_Shuffled() => SumX(_shuffled);

    /// <summary>Moves every entity to the tagged archetype and back.</summary>
    [Benchmark]
    public void AddThenRemoveTag()
    {
        for (var i = 0; i < _entities.Length; i++)
            _world.Add<Frozen>(_entities[i]);
        for (var i = 0; i < _entities.Length; i++)
            _world.Remove<Frozen>(_entities[i]);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private float SumX(Entity[] entities)
    {
        var sum = 0f;
        for (var i = 0; i < entities.Length; i++)
            sum += _world.Get<Velocity>(entities[i]).Value.X;
        return sum;
    }
}
