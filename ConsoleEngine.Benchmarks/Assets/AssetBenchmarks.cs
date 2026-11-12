using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Assets;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Benchmarks.Assets;

/// <summary>
/// Turning an id into a sprite, as a render system does once per entity per frame: <see cref="SpriteLibrary.Get"/>
/// (the baseline) against <see cref="AssetManager.Get{T}"/> with a handle and against a kept
/// <see cref="AssetStore{T}"/> (<see cref="AssetManager.GetStore{T}"/>). <c>Load_CacheHit</c> shows why
/// <c>Load</c> doesn't belong in the frame loop even when the asset is cached. One invocation looks up
/// <see cref="Count"/> ids, cycling through <see cref="Sprites"/> different sprites.
/// </summary>
[MemoryDiagnoser]
public class AssetBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int Sprites = 64;

    private SpriteLibrary _library;
    private int[] _ids;
    private AssetManager _assets;
    private AssetHandle<Sprite>[] _handles;
    private AssetStore<Sprite> _store;
    private AssetPath[] _paths;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(10_000)]
    public int Count { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        var source = new MemoryAssetSource();
        _library = new SpriteLibrary();
        _ids = new int[Sprites];
        _handles = new AssetHandle<Sprite>[Sprites];
        _paths = new AssetPath[Sprites];

        for (var i = 0; i < Sprites; i++)
        {
            _paths[i] = AssetPath.Parse($"Sprites/s{i}.sprite");
            source.Add(_paths[i], "##\n##");
        }

        _assets = new AssetManager(source);
        for (var i = 0; i < Sprites; i++)
        {
            _handles[i] = _assets.LoadHandle<Sprite>(_paths[i]);
            _ids[i] = _library.Add(_assets.Get(_handles[i]));
        }

        _store = _assets.GetStore<Sprite>();
    }

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark(Baseline = true)]
    public int SpriteLibrary_Get()
    {
        var width = 0;
        for (var i = 0; i < Count; i++)
            width += _library.Get(_ids[i & (Sprites - 1)]).Width;

        return width;
    }

    [Benchmark]
    public int AssetManager_GetHandle()
    {
        var width = 0;
        for (var i = 0; i < Count; i++)
            width += _assets.Get(_handles[i & (Sprites - 1)]).Width;

        return width;
    }

    [Benchmark]
    public int AssetStore_Get()
    {
        var store = _store;
        var width = 0;
        for (var i = 0; i < Count; i++)
            width += store.Get(_handles[i & (Sprites - 1)]).Width;

        return width;
    }

    [Benchmark]
    public int AssetManager_Load_CacheHit()
    {
        var width = 0;
        for (var i = 0; i < Count; i++)
            width += _assets.Load<Sprite>(_paths[i & (Sprites - 1)]).Width;

        return width;
    }
}
