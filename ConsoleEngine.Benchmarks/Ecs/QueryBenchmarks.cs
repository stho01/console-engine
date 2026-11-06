using BenchmarkDotNet.Attributes;
using ConsoleEngine.Ecs;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Ecs;

/// <summary>
/// The same "position += velocity * delta" frame as <c>OopUpdateBenchmarks</c>, through a query. Compare with its
/// <c>StructArrays</c> (the lower bound) and <c>ObjectList_*</c> (today's pattern).
/// </summary>
[MemoryDiagnoser]
public class QueryBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float Delta = 1f / 60f;

    private World _world;
    private Query _query;

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
        _world.Create(new Entity[Count], new Position(), new Velocity { Value = Vector2.One });
        _query = _world.Query(Query.With<Position, Velocity>());
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark(Baseline = true)]
    public void ForEach()
    {
        var job = new MoveJob { Delta = Delta };
        _query.ForEach<MoveJob, Position, Velocity>(ref job);
    }

    [Benchmark]
    public void GetChunks()
    {
        foreach (var chunk in _query.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            for (var i = 0; i < positions.Length; i++)
                positions[i].Value += velocities[i].Value * Delta;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct MoveJob : IForEach<Position, Velocity>
    {
        public float Delta;

        public void Execute(ref Position position, ref Velocity velocity) => position.Value += velocity.Value * Delta;
    }
}
