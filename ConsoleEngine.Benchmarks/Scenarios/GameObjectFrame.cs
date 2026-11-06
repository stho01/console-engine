using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Infrastructure.Objects;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Scripting;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Benchmarks.Scenarios;

/// <summary>
/// One frame (update + render) of N moving <see cref="GameObject"/>s: with a <see cref="LuaBehaviour"/> running
/// <c>Scripts/mover.lua</c>, and with <see cref="MoverBehaviour"/>, the same logic in C#. The difference is what Lua
/// scripting costs per object.
/// </summary>
[MemoryDiagnoser]
public class GameObjectFrame
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private const int KeyLeft = 0x25;
    private const int KeyRight = 0x27;
    private static readonly TimeSpan Delta = TimeSpan.FromSeconds(EngineBindings.FrameDelta);

    [Params(100, 1000)] public int ObjectCount;

    private EngineBindings _cSharpBindings;
    private GameObject[] _cSharpObjects;

    private EngineBindings _luaBindings;
    private LuaRuntime _runtime;
    private GameObject[] _luaObjects;

    private int _frame;

    //**********************************************************
    //** setup:
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _cSharpBindings = new EngineBindings();
        _cSharpObjects = CreateObjects(id => new MoverBehaviour(_cSharpBindings, id));

        _luaBindings = new EngineBindings();
        _runtime = new LuaRuntime(_luaBindings.Console, _luaBindings.GetKey);
        var script = _runtime.Load(Path.Combine(AppContext.BaseDirectory, "Scripts", "mover.lua"));
        _luaObjects = CreateObjects(_ => script.CreateBehaviour());
    }

    [GlobalCleanup]
    public void Cleanup() => _runtime.Dispose();

    //**********************************************************
    //** benchmarks:
    //**********************************************************

    [Benchmark(Baseline = true)]
    public void CSharpBehaviour() => RunFrame(_cSharpBindings, _cSharpObjects);

    [Benchmark]
    public void LuaBehaviour() => RunFrame(_luaBindings, _luaObjects);

    //**********************************************************
    //** internals:
    //**********************************************************

    private void RunFrame(EngineBindings bindings, GameObject[] objects)
    {
        bindings.BeginFrame(_frame++);
        for (var i = 0; i < objects.Length; i++)
            objects[i].Update(Delta);
        for (var i = 0; i < objects.Length; i++)
            objects[i].Render(bindings.Console);
    }

    private GameObject[] CreateObjects(Func<int, IBehaviour> behaviour)
    {
        var objects = new GameObject[ObjectCount];
        for (var i = 0; i < ObjectCount; i++)
        {
            var id = i + 1;
            objects[i] = new GameObject
            {
                Position = new Vector2(id * 7 % EngineBindings.ScreenWidth, id * 13 % EngineBindings.ScreenHeight),
                Char = (char)(64 + id % 26),
                Color = (ConsoleColor)(1 + id % 15),
                Behaviour = behaviour(id)
            };
        }

        return objects;
    }

    /// <summary><c>Scripts/mover.lua</c> in C#.</summary>
    private sealed class MoverBehaviour(EngineBindings bindings, int id) : IBehaviour
    {
        private const float Acceleration = 20;

        private float _vx = id % 5 - 2 + 0.5f;
        private float _vy = id % 3 - 1 + 0.25f;

        public void Start(GameObject owner) { }

        public void Update(GameObject owner, TimeSpan delta)
        {
            var dt = (float)delta.TotalSeconds;
            if (bindings.IsDown(KeyLeft)) _vx -= Acceleration * dt;
            if (bindings.IsDown(KeyRight)) _vx += Acceleration * dt;

            var position = owner.Position;
            var x = position.X + _vx * dt;
            var y = position.Y + _vy * dt;
            if (x < 0 || x >= EngineBindings.ScreenWidth)
            {
                _vx = -_vx;
                x = position.X;
            }
            if (y < 0 || y >= EngineBindings.ScreenHeight)
            {
                _vy = -_vy;
                y = position.Y;
            }

            owner.Position = new Vector2(x, y);
        }

        public void Render(GameObject owner, RenderConsole console) { }
    }
}
