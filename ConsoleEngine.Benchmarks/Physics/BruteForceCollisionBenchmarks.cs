using System;
using BenchmarkDotNet.Attributes;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Physics;

/// <summary>
/// Baseline for the physics broadphase (<c>docs/design/physics-plan.md</c>, phase 0): every circle tested against
/// every other circle, as <c>Examples/Asteroids.Ecs/Systems/CollisionSystem</c> does today, but on plain arrays. That
/// is the lower bound for brute force; a broadphase has to beat it from a few hundred bodies on.
/// <para>
/// The world grows with <see cref="Count"/> so the density stays the same: on average about one cell in
/// <see cref="CellsPerBody"/> holds a body, as in a busy console game.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class BruteForceCollisionBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int CellsPerBody = 16;
    private const float MinRadius = .5f;
    private const float MaxRadius = 1.5f;

    private Vector2[] _positions;
    private float[] _radii;

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
        var random = new Random(42);
        var side = MathF.Sqrt(Count * CellsPerBody);
        _positions = new Vector2[Count];
        _radii = new float[Count];
        for (var i = 0; i < Count; i++)
        {
            _positions[i] = new Vector2(random.NextSingle() * side, random.NextSingle() * side);
            _radii[i] = MinRadius + random.NextSingle() * (MaxRadius - MinRadius);
        }
    }

    //**********************************************************
    //** benchmarks
    //**********************************************************

    /// <summary>All n·(n−1)/2 pairs. Returns the overlap count so the work can't be optimized away.</summary>
    [Benchmark(Baseline = true)]
    public int AllPairs()
    {
        var positions = _positions.AsSpan();
        var radii = _radii.AsSpan();
        var overlaps = 0;
        for (var a = 0; a < positions.Length; a++)
        {
            var position = positions[a];
            var radius = radii[a];
            for (var b = a + 1; b < positions.Length; b++)
            {
                var reach = radius + radii[b];
                if (Vector2.DistanceSquared(position, positions[b]) <= reach * reach)
                    overlaps++;
            }
        }

        return overlaps;
    }
}
