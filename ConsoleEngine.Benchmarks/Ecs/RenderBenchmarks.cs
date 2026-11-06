using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using EcsComponents = ConsoleEngine.Ecs.Components;

namespace ConsoleEngine.Benchmarks.Ecs;

/// <summary>
/// The built-in render systems drawing <see cref="Count"/> entities at random positions on a 200×200 console (the
/// size Asteroids uses). One invocation is one frame of that system. Nothing reaches a real console.
/// </summary>
[MemoryDiagnoser]
public class RenderBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int ScreenSize = 200;

    private World _world;
    private SystemGroup _glyphs;
    private SystemGroup _sprites;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(1_000, 10_000, 100_000)]
    public int Count { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        var console = new RenderConsole(new NullConsoleHandler(ScreenSize, ScreenSize));
        var camera = new Camera();
        var library = new SpriteLibrary();
        var sprite = library.Add(Sprite.FromStringArray(["/¯¯\\", "║##║", "└──┘"], ConsoleColor.Green));
        var random = new Random(42);

        _world = new World(Count * 2);
        for (var i = 0; i < Count; i++)
        {
            var position = new Vector2(random.Next(-2, ScreenSize), random.Next(-2, ScreenSize));
            _world.Create(new EcsComponents.Position(position), new EcsComponents.Glyph('*', ConsoleColor.Yellow));
            _world.Create(new EcsComponents.Position(position), new EcsComponents.SpriteRef(sprite));
        }

        _glyphs = new SystemGroup(_world);
        _glyphs.Add(new GlyphRenderSystem(console, camera));
        _sprites = new SystemGroup(_world);
        _sprites.Add(new SpriteRenderSystem(console, camera, library));
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark]
    public void Glyphs() => _glyphs.Update(0f);

    /// <summary>4×3 sprites, some partly off the top or left edge.</summary>
    [Benchmark]
    public void Sprites() => _sprites.Update(0f);

    //**********************************************************
    //** internals
    //**********************************************************

    private sealed class NullConsoleHandler(int width, int height) : IConsoleHandler
    {
        public int Width { get; } = width;
        public int Height { get; } = height;

        public void InitializeConsole() { }
        public void SetTitle(string title) { }
        public void SetCursorVisible(bool visible) { }
        public void Resizable(bool resizable) { }
        public void SetFont(FontInfo fontInfo) { }
        public void Render(Span<Pixel> pixels) { }
        public void Close() { }
    }
}
