using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Baseline;

/// <summary>
/// Baseline for the ECS: moving N objects the way the example games do it today (a list of classes with a virtual
/// Update), next to plain struct arrays, which are the lower bound an ECS can reach.
/// One invocation updates all <see cref="Count"/> objects once, like one frame.
/// </summary>
[MemoryDiagnoser]
public class OopUpdateBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float Delta = 1f / 60f;

    private List<GameObject> _sequential;
    private List<GameObject> _shuffled;
    private Vector2[] _positions;
    private Vector2[] _velocities;

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
        _sequential = new List<GameObject>(Count);
        _positions = new Vector2[Count];
        _velocities = new Vector2[Count];

        for (var i = 0; i < Count; i++)
        {
            var velocity = new Vector2(i % 7, i % 11);
            _sequential.Add(new Mover { Velocity = velocity });
            _velocities[i] = velocity;
        }

        // Objects spawned and destroyed over time end up scattered on the heap. A shuffled list models that.
        _shuffled = new List<GameObject>(_sequential);
        new Random(42).Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_shuffled));
    }

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark(Baseline = true)]
    public void ObjectList_Sequential() => Update(_sequential);

    [Benchmark]
    public void ObjectList_Shuffled() => Update(_shuffled);

    [Benchmark]
    public void StructArrays()
    {
        var positions = _positions;
        var velocities = _velocities;
        for (var i = 0; i < positions.Length; i++)
            positions[i] += velocities[i] * Delta;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Update(List<GameObject> objects)
    {
        for (var i = 0; i < objects.Count; i++)
            objects[i].Update(Delta);
    }

    private abstract class GameObject
    {
        public Vector2 Position;
        public abstract void Update(float delta);
    }

    private sealed class Mover : GameObject
    {
        public Vector2 Velocity;
        public override void Update(float delta) => Position += Velocity * delta;
    }
}
