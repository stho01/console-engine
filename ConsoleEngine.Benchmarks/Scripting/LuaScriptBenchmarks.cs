using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Scripting;
using Microsoft.Xna.Framework;
using EcsComponents = ConsoleEngine.Ecs.Components;

namespace ConsoleEngine.Benchmarks.Scripting;

/// <summary>
/// One frame (simulation + presentation) of <see cref="Count"/> moving entities on a 200×200 console: steered by
/// <c>mover.lua</c> through <see cref="LuaScriptSystem"/>, or by <see cref="MoverSystem"/>, the same logic in C#.
/// <see cref="GlyphRenderSystem"/> draws both. The difference is what Lua scripting costs per entity.
/// </summary>
[MemoryDiagnoser]
public class LuaScriptBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int ScreenSize = 200;
    private const float Delta = 1f / 60f;
    private const int KeyLeft = 0x25;
    private const int KeyRight = 0x27;

    private readonly bool[] _keys = new bool[256];
    private RenderConsole _console;
    private LuaRuntime _runtime;
    private Frame _cSharp;
    private Frame _lua;
    private int _frame;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(100, 1_000)]
    public int Count { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _console = new RenderConsole(new NullConsoleHandler(ScreenSize, ScreenSize));
        _runtime = new LuaRuntime(_console, key => new KeyState { Held = _keys[key] });
        var mover = _runtime.Load(Path.Combine(AppContext.BaseDirectory, "Scripting", "mover.lua"));

        _cSharp = new Frame(_console);
        _cSharp.Simulation.Add(new MoverSystem(_keys));
        CreateMovers(_cSharp.World, null);

        _lua = new Frame(_console);
        _lua.Simulation.Add(new LuaScriptSystem(_runtime));
        CreateMovers(_lua.World, mover);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cSharp.World.Dispose();
        _lua.World.Dispose();
        _runtime.Dispose();
    }

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark(Baseline = true)]
    public void CSharpSystem() => Run(_cSharp);

    [Benchmark]
    public void LuaScripts() => Run(_lua);

    //**********************************************************
    //** internals
    //**********************************************************

    private void Run(Frame frame)
    {
        // Same deterministic key pattern for both.
        _keys[KeyLeft] = _frame % 4 == 0;
        _keys[KeyRight] = _frame % 3 == 0;
        _frame++;

        frame.Simulation.Update(Delta);
        frame.Presentation.Update(Delta);
    }

    private void CreateMovers(World world, LuaScript script)
    {
        for (var id = 1; id <= Count; id++)
        {
            var position = new EcsComponents.Position(new Vector2(id * 7 % ScreenSize, id * 13 % ScreenSize));
            var velocity = new EcsComponents.Velocity(new Vector2(id % 5 - 2 + .5f, id % 3 - 1 + .25f));
            var glyph = new EcsComponents.Glyph((char)(64 + id % 26), (ConsoleColor)(1 + id % 15));
            if (script == null)
                world.Create(position, velocity, glyph);
            else
                world.Create(position, velocity, glyph, script.Component);
        }
    }

    private sealed class Frame
    {
        public Frame(RenderConsole console)
        {
            World = new World();
            Simulation = new SystemGroup(World);
            Presentation = new SystemGroup(World);
            Presentation.Add(new GlyphRenderSystem(console, new Camera()));
        }

        public World World { get; }
        public SystemGroup Simulation { get; }
        public SystemGroup Presentation { get; }
    }

    /// <summary><c>mover.lua</c> in C#.</summary>
    private sealed class MoverSystem(bool[] keys) : EntitySystem
    {
        private const float Acceleration = 20;

        private Query _movers;

        protected override void OnCreate() =>
            _movers = World.Query(Query.With<EcsComponents.Position, EcsComponents.Velocity>());

        protected override void OnUpdate(float deltaSeconds)
        {
            var acceleration = (keys[KeyRight] ? Acceleration : 0) - (keys[KeyLeft] ? Acceleration : 0);
            foreach (var chunk in _movers.GetChunks())
            {
                var positions = chunk.GetSpan<EcsComponents.Position>();
                var velocities = chunk.GetSpan<EcsComponents.Velocity>();
                for (var i = 0; i < chunk.Count; i++)
                {
                    ref var p = ref positions[i].Value;
                    ref var v = ref velocities[i].Value;
                    v.X += acceleration * deltaSeconds;

                    var x = p.X + v.X * deltaSeconds;
                    var y = p.Y + v.Y * deltaSeconds;
                    if (x < 0 || x >= ScreenSize)
                    {
                        v.X = -v.X;
                        x = p.X;
                    }
                    if (y < 0 || y >= ScreenSize)
                    {
                        v.Y = -v.Y;
                        y = p.Y;
                    }

                    p = new Vector2(x, y);
                }
            }
        }
    }

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
