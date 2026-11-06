using System;
using System.IO;
using KeraLua;

namespace ConsoleEngine.Scripting;

/// <summary>
/// A loaded script file. The file returns a table (the module) with any of <c>on_start(self)</c>,
/// <c>on_update(self, dt)</c> and <c>on_render(self)</c>. Put <see cref="Component"/> on an entity to run it there:
/// each entity gets its own <c>self</c> table whose metatable falls back to the module, so module functions work as
/// methods (<c>self:jump()</c>).
/// </summary>
public sealed class LuaScript
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private readonly LuaRuntime _runtime;
    private int _module = LuaRuntime.NoRef;

    //**********************************************************
    //** ctor:
    //**********************************************************

    internal LuaScript(LuaRuntime runtime, int id, string name, string fullPath)
    {
        _runtime = runtime;
        Id = id;
        Name = name;
        FullPath = fullPath;
    }

    //**********************************************************
    //** props:
    //**********************************************************

    /// <summary>Index in the runtime's scripts, stored in <see cref="Script.Id"/>.</summary>
    public int Id { get; }

    /// <summary>The component that runs this script on an entity.</summary>
    public Script Component => new(Id);

    /// <summary>The path as passed to <see cref="LuaRuntime.Load"/>. Error messages start with it.</summary>
    public string Name { get; }
    public string FullPath { get; }

    /// <summary>Incremented by every successful <see cref="Reload"/>.</summary>
    public int Version { get; private set; }

    internal int Metatable { get; private set; } = LuaRuntime.NoRef;
    internal int OnStart { get; private set; } = LuaRuntime.NoRef;
    internal int OnUpdate { get; private set; } = LuaRuntime.NoRef;
    internal int OnRender { get; private set; } = LuaRuntime.NoRef;

    //**********************************************************
    //** public methods:
    //**********************************************************

    /// <summary>
    /// Runs the file again and copies the new module's fields into the existing module, so entities that already run
    /// the script use the new functions. Their <c>self</c> tables are kept. On failure the previous version stays.
    /// </summary>
    /// <exception cref="LuaScriptException">The file is missing, has an error or does not return a table.</exception>
    public void Reload()
    {
        var lua = _runtime.State;
        var top = lua.GetTop();

        if (lua.LoadBuffer(ReadSource(), "@" + Name) != LuaStatus.OK)
        {
            var message = lua.ToString(-1);
            lua.SetTop(top);
            throw new LuaScriptException(Name, message);
        }

        var error = _runtime.Call(0, 1);
        if (error != null)
            throw new LuaScriptException(Name, error);

        if (lua.Type(-1) != LuaType.Table)
        {
            lua.SetTop(top);
            throw new LuaScriptException(Name, "the script must return a table");
        }

        if (_module == LuaRuntime.NoRef)
            CreateModule();
        else
            UpdateModule();

        OnStart = CacheFunction("on_start", OnStart);
        OnUpdate = CacheFunction("on_update", OnUpdate);
        OnRender = CacheFunction("on_render", OnRender);
        Version++;
        _runtime.OnReloaded();
    }

    //**********************************************************
    //** internals:
    //**********************************************************

    private byte[] ReadSource()
    {
        byte[] source;
        try
        {
            source = File.ReadAllBytes(FullPath);
        }
        catch (IOException e)
        {
            throw new LuaScriptException(Name, e.Message);
        }

        // Lua's loadbuffer does not skip a UTF-8 byte order mark, unlike loadfile.
        return source.AsSpan().StartsWith("﻿"u8) ? source[3..] : source;
    }

    /// <summary>
    /// Stack: [module] → []. Keeps the module and the metatable for its entities' <c>self</c>:
    /// <c>{ __index = module, __newindex = unknown-field guard }</c>.
    /// </summary>
    private void CreateModule()
    {
        var lua = _runtime.State;
        _module = _runtime.Ref();

        lua.CreateTable(0, 2);
        _runtime.Push(_module);
        lua.SetField(-2, "__index");
        _runtime.Push(_runtime.UnknownFieldGuard);
        lua.SetField(-2, "__newindex");
        Metatable = _runtime.Ref();
    }

    /// <summary>Stack: [new module] → []. Copies every field of the new module into the existing one.</summary>
    private void UpdateModule()
    {
        var lua = _runtime.State;
        _runtime.Push(_module);  // [new, module]
        lua.PushNil();           // [new, module, nil]
        while (lua.Next(-3))     // [new, module, key, value]
        {
            lua.PushCopy(-2);    // [new, module, key, value, key]
            lua.PushCopy(-2);    // [new, module, key, value, key, value]
            lua.RawSet(-5);      // [new, module, key, value]
            lua.Pop(1);          // [new, module, key]
        }
        lua.Pop(2);
    }

    private int CacheFunction(string name, int previous)
    {
        var lua = _runtime.State;
        _runtime.Unref(previous);
        _runtime.Push(_module);
        if (lua.GetField(-1, name) != LuaType.Function)
        {
            lua.Pop(2);
            return LuaRuntime.NoRef;
        }

        var reference = _runtime.Ref();
        lua.Pop(1);
        return reference;
    }
}
