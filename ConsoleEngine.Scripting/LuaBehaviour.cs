using System;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Infrastructure.Objects;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Scripting;

/// <summary>
/// Runs a <see cref="LuaScript"/> as the behaviour of one <see cref="GameObject"/>. Create it with
/// <see cref="LuaScript.CreateBehaviour"/>. Neither hook call allocates.
/// </summary>
/// <remarks>
/// <para>
/// The script sees the object as <c>self</c> with a fixed set of engine fields: <c>x</c>, <c>y</c>, <c>char</c>,
/// <c>color</c>, <c>background</c>, <c>visible</c>, plus <c>data</c>, a table the script owns. The engine fields are
/// copied into <c>self</c> before every hook and back into the <see cref="GameObject"/> after <c>on_start</c> and
/// <c>on_update</c>. Writing any other field on <c>self</c> is an error.
/// </para>
/// <para>
/// A Lua error faults the behaviour: <see cref="Error"/> is set, the error is logged, and the hooks stop running until
/// the script is reloaded. The game object keeps its last good state and is still drawn.
/// </para>
/// </remarks>
public sealed class LuaBehaviour : IBehaviour, IDisposable
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private readonly LuaRuntime _runtime;
    private int _self;
    private int _faultedVersion;

    //**********************************************************
    //** ctor:
    //**********************************************************

    internal LuaBehaviour(LuaRuntime runtime, LuaScript script, int self)
    {
        _runtime = runtime;
        Script = script;
        _self = self;
    }

    //**********************************************************
    //** props:
    //**********************************************************

    public LuaScript Script { get; }

    /// <summary>The Lua error that stopped this behaviour, or <c>null</c>.</summary>
    public string Error { get; private set; }

    public bool IsFaulted => Error != null;

    //**********************************************************
    //** public methods:
    //**********************************************************

    /// <summary>Calls <c>on_start(self)</c>.</summary>
    public void Start(GameObject owner)
    {
        if (CanRun() && Script.OnStart != LuaRuntime.NoRef)
            Run(Script.OnStart, owner, hasDelta: false, 0, readBack: true);
    }

    /// <summary>Calls <c>on_update(self, dt)</c> with <paramref name="delta"/> in seconds.</summary>
    public void Update(GameObject owner, TimeSpan delta)
    {
        if (CanRun() && Script.OnUpdate != LuaRuntime.NoRef)
            Run(Script.OnUpdate, owner, hasDelta: true, delta.TotalSeconds, readBack: true);
    }

    /// <summary>Calls <c>on_render(self)</c>. Changes to the engine fields are not copied back.</summary>
    public void Render(GameObject owner, RenderConsole console)
    {
        if (CanRun() && Script.OnRender != LuaRuntime.NoRef)
            Run(Script.OnRender, owner, hasDelta: false, 0, readBack: false);
    }

    public void Dispose()
    {
        _runtime.Unref(_self);
        _self = LuaRuntime.NoRef;
    }

    //**********************************************************
    //** internals:
    //**********************************************************

    private bool CanRun()
    {
        if (_self == LuaRuntime.NoRef)
            return false;

        // The script was reloaded since the error: give the new version a chance.
        if (Error != null && _faultedVersion != Script.Version)
            Error = null;

        return Error == null;
    }

    private void Run(int function, GameObject owner, bool hasDelta, double delta, bool readBack)
    {
        _runtime.WriteFields(_self, owner);
        _runtime.Push(function);
        _runtime.Push(_self);
        if (hasDelta)
            _runtime.State.PushNumber(delta);

        var error = _runtime.Call(hasDelta ? 2 : 1, 0);
        if (error == null && readBack)
            error = _runtime.ReadFields(_self, owner);

        if (error != null)
            Fault(error);
    }

    private void Fault(string error)
    {
        Error = error;
        _faultedVersion = Script.Version;
        Log.WriteLine($"Lua: {error}");
    }
}
