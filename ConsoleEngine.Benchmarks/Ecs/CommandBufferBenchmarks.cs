using BenchmarkDotNet.Attributes;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Ecs;

/// <summary>
/// The same structural changes made directly on the <see cref="World"/> and through a <see cref="CommandBuffer"/>.
/// The difference is the cost of deferring: recording, then replaying from bytes. One invocation touches all
/// <see cref="Count"/> entities.
/// </summary>
[MemoryDiagnoser]
public class CommandBufferBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private World _world;
    private CommandBuffer _commands;
    private Query _spawned;
    private Entity[] _entities;
    private Entity[] _scratch;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(10_000, 100_000)]
    public int Count { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _world = new World(Count * 2);
        _commands = new CommandBuffer(Count * 64);
        _entities = new Entity[Count];
        _scratch = new Entity[Count];
        _world.Create(_entities, new Position(), new Velocity { Value = Vector2.One });
        _spawned = _world.Query(Query.With<Position>().Without<Velocity>());
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark(Baseline = true)]
    public void CreateThenDestroy_Direct()
    {
        for (var i = 0; i < _scratch.Length; i++)
            _scratch[i] = _world.Create(new Position());
        for (var i = 0; i < _scratch.Length; i++)
            _world.Destroy(_scratch[i]);
    }

    [Benchmark]
    public void CreateThenDestroy_Buffered()
    {
        for (var i = 0; i < Count; i++)
            _commands.Create(new Position());
        _commands.Playback(_world);

        foreach (var chunk in _spawned.GetChunks())
        {
            foreach (var entity in chunk.Entities)
                _commands.Destroy(entity);
        }

        _commands.Playback(_world);
    }

    [Benchmark]
    public void AddThenRemoveTag_Direct()
    {
        for (var i = 0; i < _entities.Length; i++)
            _world.Add<Frozen>(_entities[i]);
        for (var i = 0; i < _entities.Length; i++)
            _world.Remove<Frozen>(_entities[i]);
    }

    [Benchmark]
    public void AddThenRemoveTag_Buffered()
    {
        for (var i = 0; i < _entities.Length; i++)
            _commands.Add<Frozen>(_entities[i]);
        _commands.Playback(_world);
        for (var i = 0; i < _entities.Length; i++)
            _commands.Remove<Frozen>(_entities[i]);
        _commands.Playback(_world);
    }
}
