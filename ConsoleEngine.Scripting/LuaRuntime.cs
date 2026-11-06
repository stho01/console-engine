using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Scripting.LowLevel;
using KeraLua;
using Microsoft.Xna.Framework;
using LuaState = KeraLua.Lua;

namespace ConsoleEngine.Scripting;

/// <summary>
/// One native Lua 5.4 state with the engine API registered: <c>input</c>, <c>console</c>, <c>keys</c>, <c>colors</c>
/// and <c>print</c> (to <see cref="Log"/>). Load scripts with <see cref="Load"/>, put
/// <see cref="LuaScript.Component"/> on entities, and run them with <see cref="LuaScriptSystem"/>. Not thread-safe:
/// use it from the game-loop thread only.
/// </summary>
/// <remarks>
/// Host functions never raise Lua errors: <c>lua_error</c> would long-jump across managed frames. A .NET exception in a
/// host function is stored and reported as the error of the current call when it returns.
/// </remarks>
public sealed class LuaRuntime : IDisposable
{
    //**********************************************************
    //** fields:
    //**********************************************************

    internal const int NoRef = -2; // LUA_NOREF

    // Bits of the mask passed to the hook trampoline: which components the entity has, and whether to read back.
    internal const int HasPosition = 1;
    internal const int HasVelocity = 2;
    internal const int HasGlyph = 4;
    internal const int ReadBack = 8;
    internal const int ComponentBits = HasPosition | HasVelocity | HasGlyph;

    private const int KeyCount = 256; // size of the key tables in InputHandler
    private const int MaxTextLength = 256;
    private const int HookArguments = 11;
    private const int HookResults = 7;

    // Static so native code always points to a live delegate. Each closure finds its runtime through upvalue 1.
    private static readonly LuaFunction HeldFunction = Held;
    private static readonly LuaFunction PressedFunction = Pressed;
    private static readonly LuaFunction ReleasedFunction = Released;
    private static readonly LuaFunction DrawFunction = Draw;
    private static readonly LuaFunction TextFunction = Text;
    private static readonly LuaFunction PrintFunction = Print;

    /// <summary>
    /// Engine-side Lua, compiled once. <c>guard</c> is the <c>__newindex</c> of every entity's <c>self</c>.
    /// <c>run</c> copies the component fields into <c>self</c>, calls the hook and returns the fields, so a hook costs
    /// one native call instead of one per field. It uses plain field access, not <c>rawset</c>/<c>rawget</c> (each a
    /// C call): a field that already exists never reaches <c>guard</c>, and one that doesn't is let through while
    /// <c>syncing</c>. <c>clear</c> removes the fields of components the entity lost.
    /// </summary>
    private const string EngineSource = """
        local rawset, error, tostring = rawset, error, tostring
        local COMPONENT = { x = "Position", y = "Position", vx = "Velocity", vy = "Velocity",
                            char = "Glyph", color = "Glyph", background = "Glyph" }
        local syncing = false

        local function guard(self, key, value)
            if syncing then
                rawset(self, key, value)
                return
            end
            local component = COMPONENT[key]
            if component then
                error("self." .. key .. " needs a " .. component .. " component on the entity", 2)
            end
            error("unknown field '" .. tostring(key) .. "' on an entity, put script data in self.data", 2)
        end

        local function run(hook, self, dt, mask, x, y, vx, vy, char, color, background)
            syncing = true
            if mask & 1 ~= 0 then self.x = x; self.y = y end
            if mask & 2 ~= 0 then self.vx = vx; self.vy = vy end
            if mask & 4 ~= 0 then self.char = char; self.color = color; self.background = background end
            syncing = false
            hook(self, dt)
            if mask & 8 == 0 then return end
            return self.x, self.y, self.vx, self.vy, self.char, self.color, self.background
        end

        local function clear(self, mask)
            if mask & 1 ~= 0 then rawset(self, "x", nil); rawset(self, "y", nil) end
            if mask & 2 ~= 0 then rawset(self, "vx", nil); rawset(self, "vy", nil) end
            if mask & 4 ~= 0 then
                rawset(self, "char", nil); rawset(self, "color", nil); rawset(self, "background", nil)
            end
        end

        return guard, run, clear
        """;

    private readonly LuaState _lua;
    private readonly GCHandle _handle;
    private readonly RenderConsole _console;
    private readonly Func<int, KeyState> _getKey;
    private readonly List<LuaScript> _scripts = new();
    private readonly int _run;
    private readonly int _clear;
    private readonly int _keyData;
    private Exception _hostError;

    //**********************************************************
    //** ctor:
    //**********************************************************

    public LuaRuntime(GameBase game) : this(game.Console, key => Input.Instance.GetKey(key)) { }

    internal LuaRuntime(RenderConsole console, Func<int, KeyState> getKey)
    {
        _console = console;
        _getKey = getKey;
        _lua = new LuaState();
        _lua.OpenLibs();
        _handle = GCHandle.Alloc(this);
        RegisterApi();

        _lua.PushString("data");
        _keyData = Ref();

        _lua.LoadString(EngineSource, "=engine");
        Call(0, 3);
        _clear = Ref();
        _run = Ref();
        UnknownFieldGuard = Ref();
    }

    //**********************************************************
    //** props:
    //**********************************************************

    /// <summary>Incremented by every successful <see cref="LuaScript.Reload"/> of any script.</summary>
    public int ReloadCount { get; private set; }

    internal LuaState State => _lua;

    /// <summary>Registry ref to the <c>__newindex</c> function of every entity's <c>self</c>.</summary>
    internal int UnknownFieldGuard { get; }

    //**********************************************************
    //** public methods:
    //**********************************************************

    /// <summary>
    /// Runs the script file and keeps the table it returns as its module. A relative path is resolved against the
    /// game's output folder (<see cref="AppContext.BaseDirectory"/>), not the working directory.
    /// </summary>
    /// <exception cref="LuaScriptException">The file is missing, has an error or does not return a table.</exception>
    public LuaScript Load(string path)
    {
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        var script = new LuaScript(this, _scripts.Count, path, fullPath);
        script.Reload();
        _scripts.Add(script);
        return script;
    }

    /// <summary>Reloads every loaded script. A script that fails keeps its previous version.</summary>
    /// <returns>The failures, empty when every script reloaded.</returns>
    public LuaScriptException[] ReloadAll()
    {
        var failures = new List<LuaScriptException>();
        foreach (var script in _scripts)
        {
            try
            {
                script.Reload();
            }
            catch (LuaScriptException e)
            {
                failures.Add(e);
            }
        }

        return failures.ToArray();
    }

    public void Dispose()
    {
        _lua.Dispose();
        if (_handle.IsAllocated)
            _handle.Free();
    }

    //**********************************************************
    //** internals:
    //**********************************************************

    internal LuaScript GetScript(int id) => _scripts[id];

    internal void OnReloaded() => ReloadCount++;

    internal void Push(int reference) => _lua.RawGetInteger(LuaRegistry.Index, reference);

    /// <summary>Pops the top value into the registry and returns its reference.</summary>
    internal int Ref() => _lua.Ref(LuaRegistry.Index);

    internal void Unref(int reference)
    {
        if (reference > 0)
            _lua.Unref(LuaRegistry.Index, reference);
    }

    /// <summary>Calls the function below its <paramref name="arguments"/> on the stack.</summary>
    /// <returns><c>null</c> on success (the results are on the stack), otherwise the error (the stack is clean).</returns>
    internal string Call(int arguments, int results)
    {
        string error = null;
        if (_lua.PCall(arguments, results, 0) != LuaStatus.OK)
        {
            error = _lua.ToString(-1);
            _lua.Pop(1);
        }
        else if (_hostError != null)
        {
            _lua.Pop(results);
        }

        if (_hostError != null)
        {
            error = $"{_hostError.GetType().Name}: {_hostError.Message}";
            _hostError = null;
        }

        return error;
    }

    /// <summary>Creates an entity's <c>self</c>: <c>{ data = {} }</c> with the script's metatable.</summary>
    /// <returns>Its registry reference.</returns>
    internal int CreateSelf(LuaScript script)
    {
        _lua.CreateTable(0, 8);
        Push(_keyData);
        _lua.NewTable();
        _lua.RawSet(-3);
        Push(script.Metatable);
        _lua.SetMetaTable(-2);
        return Ref();
    }

    /// <summary>Removes the fields of the components in <paramref name="mask"/> from <c>self</c>.</summary>
    internal void ClearFields(int self, int mask)
    {
        Push(_clear);
        Push(self);
        _lua.PushInteger(mask);
        Call(2, 0);
    }

    /// <summary>
    /// Calls <paramref name="hook"/> with <c>self</c> and <paramref name="deltaSeconds"/>. The fields of the components
    /// in <paramref name="mask"/> are copied into <c>self</c> first and, with <see cref="ReadBack"/>, back into the
    /// components afterwards. Components missing from the mask are passed as dummies and never written.
    /// </summary>
    /// <returns><c>null</c>, or the error. A failed hook or a field of the wrong type changes no component.</returns>
    internal string RunHook(int hook, int self, float deltaSeconds, int mask,
        ref Position position, ref Velocity velocity, ref Glyph glyph)
    {
        var top = _lua.GetTop();
        Push(_run);
        Push(hook);
        Push(self);
        _lua.PushNumber(deltaSeconds);
        _lua.PushInteger(mask);
        _lua.PushNumber(position.Value.X);
        _lua.PushNumber(position.Value.Y);
        _lua.PushNumber(velocity.Value.X);
        _lua.PushNumber(velocity.Value.Y);
        PushChar(glyph.Character);
        _lua.PushInteger((int)glyph.Foreground);
        _lua.PushInteger((int)glyph.Background);

        var readBack = (mask & ReadBack) != 0;
        var error = Call(HookArguments, readBack ? HookResults : 0);
        if (error != null || !readBack)
            return error;

        error = ReadResults(top + 1, mask, ref position, ref velocity, ref glyph);
        _lua.SetTop(top);
        return error;
    }

    /// <summary>Validates every returned field, then writes them. Stack slots: x, y, vx, vy, char, color, background.
    /// </summary>
    private string ReadResults(int first, int mask, ref Position position, ref Velocity velocity, ref Glyph glyph)
    {
        double x = 0, y = 0, vx = 0, vy = 0, color = 0, background = 0;
        var character = '\0';

        if ((mask & HasPosition) != 0)
        {
            if (!TryGetNumber(first, out x)) return "self.x must be a number";
            if (!TryGetNumber(first + 1, out y)) return "self.y must be a number";
        }

        if ((mask & HasVelocity) != 0)
        {
            if (!TryGetNumber(first + 2, out vx)) return "self.vx must be a number";
            if (!TryGetNumber(first + 3, out vy)) return "self.vy must be a number";
        }

        if ((mask & HasGlyph) != 0)
        {
            if (!TryGetChar(first + 4, out character)) return "self.char must be a string or a code point";
            if (!TryGetNumber(first + 5, out color)) return "self.color must be a number";
            if (!TryGetNumber(first + 6, out background)) return "self.background must be a number";
        }

        if ((mask & HasPosition) != 0)
            position.Value = new Vector2((float)x, (float)y);
        if ((mask & HasVelocity) != 0)
            velocity.Value = new Vector2((float)vx, (float)vy);
        if ((mask & HasGlyph) != 0)
        {
            glyph.Character = character;
            glyph.Foreground = (ConsoleColor)((int)color & 0xF);
            glyph.Background = (ConsoleColor)((int)background & 0xF);
        }

        return null;
    }

    private bool TryGetNumber(int index, out double value)
    {
        var isNumber = _lua.Type(index) == LuaType.Number;
        value = isNumber ? _lua.ToNumber(index) : 0;
        return isNumber;
    }

    /// <summary>Reads a string (first character) or a code point.</summary>
    private bool TryGetChar(int index, out char value)
    {
        var type = _lua.Type(index);
        value = type switch
        {
            LuaType.String => FirstChar(_lua, index),
            LuaType.Number => (char)_lua.ToInteger(index),
            _ => '\0'
        };
        return type is LuaType.String or LuaType.Number;
    }

    private unsafe void PushChar(char value)
    {
        if (!Rune.TryCreate(value, out var rune))
            rune = new Rune('?');

        var bytes = stackalloc byte[4];
        var length = rune.EncodeToUtf8(new Span<byte>(bytes, 4));
        LuaNative.lua_pushlstring(_lua.Handle, bytes, (UIntPtr)length);
    }

    private void RegisterApi()
    {
        _lua.CreateTable(0, 3);
        SetFunction("held", HeldFunction);
        SetFunction("pressed", PressedFunction);
        SetFunction("released", ReleasedFunction);
        _lua.SetGlobal("input");

        _lua.CreateTable(0, 4);
        SetFunction("draw", DrawFunction);
        SetFunction("text", TextFunction);
        _lua.PushInteger(_console.Width);
        _lua.SetField(-2, "width");
        _lua.PushInteger(_console.Height);
        _lua.SetField(-2, "height");
        _lua.SetGlobal("console");

        SetEnumTable<Key>("keys");
        SetEnumTable<ConsoleColor>("colors");

        PushFunction(PrintFunction);
        _lua.SetGlobal("print");
    }

    private void PushFunction(LuaFunction function)
    {
        _lua.PushLightUserData(GCHandle.ToIntPtr(_handle));
        _lua.PushCClosure(function, 1);
    }

    private void SetFunction(string name, LuaFunction function)
    {
        PushFunction(function);
        _lua.SetField(-2, name);
    }

    private void SetEnumTable<TEnum>(string name) where TEnum : struct, Enum
    {
        var names = Enum.GetNames<TEnum>();
        _lua.CreateTable(0, names.Length);
        foreach (var valueName in names)
        {
            _lua.PushInteger(Convert.ToInt64(Enum.Parse<TEnum>(valueName)));
            _lua.SetField(-2, valueName);
        }
        _lua.SetGlobal(name);
    }

    //**********************************************************
    //** host functions:
    //**********************************************************

    private enum KeyQuery { Held, Pressed, Released }

    private static int Held(IntPtr state) => PushKeyState(state, KeyQuery.Held);
    private static int Pressed(IntPtr state) => PushKeyState(state, KeyQuery.Pressed);
    private static int Released(IntPtr state) => PushKeyState(state, KeyQuery.Released);

    /// <summary><c>input.held(key)</c>, <c>input.pressed(key)</c>, <c>input.released(key)</c>.</summary>
    private static int PushKeyState(IntPtr state, KeyQuery query)
    {
        var lua = LuaState.FromIntPtr(state);
        var runtime = RuntimeOf(lua);
        try
        {
            var key = lua.ToInteger(1);
            if (key is < 0 or >= KeyCount)
            {
                lua.PushBoolean(false);
                return 1;
            }

            var keyState = runtime._getKey((int)key);
            lua.PushBoolean(query switch
            {
                KeyQuery.Held => keyState.Held,
                KeyQuery.Pressed => keyState.Pressed,
                _ => keyState.Released
            });
            return 1;
        }
        catch (Exception e)
        {
            runtime._hostError ??= e;
            return 0;
        }
    }

    /// <summary><c>console.draw(x, y, char, fg, bg)</c>: char is a string (first character) or a code point.</summary>
    private static int Draw(IntPtr state)
    {
        var lua = LuaState.FromIntPtr(state);
        var runtime = RuntimeOf(lua);
        try
        {
            var character = lua.Type(3) == LuaType.String ? FirstChar(lua, 3) : (char)lua.ToInteger(3);
            runtime._console.Draw(Coordinate(lua, 1), Coordinate(lua, 2), character,
                Color(lua, 4, ConsoleColor.White), Color(lua, 5, ConsoleColor.Black));
            return 0;
        }
        catch (Exception e)
        {
            runtime._hostError ??= e;
            return 0;
        }
    }

    /// <summary><c>console.text(x, y, text, fg, bg)</c>: draws a string or number, at most 256 characters.</summary>
    private static int Text(IntPtr state)
    {
        var lua = LuaState.FromIntPtr(state);
        var runtime = RuntimeOf(lua);
        try
        {
            if (!lua.IsStringOrNumber(3))
                return 0;

            Span<char> chars = stackalloc char[MaxTextLength];
            Utf8.ToUtf16(Utf8Bytes(lua, 3), chars, out _, out var length);

            var x = Coordinate(lua, 1);
            var y = Coordinate(lua, 2);
            var foreground = Color(lua, 4, ConsoleColor.White);
            var background = Color(lua, 5, ConsoleColor.Black);
            for (var i = 0; i < length; i++)
                runtime._console.Draw(x + i, y, chars[i], foreground, background);
            return 0;
        }
        catch (Exception e)
        {
            runtime._hostError ??= e;
            return 0;
        }
    }

    /// <summary><c>print(...)</c>: writes to the logger window. Allocates, so use it for debugging only.</summary>
    private static int Print(IntPtr state)
    {
        var lua = LuaState.FromIntPtr(state);
        var runtime = RuntimeOf(lua);
        try
        {
            var message = new StringBuilder("Lua: ");
            var count = lua.GetTop();
            for (var i = 1; i <= count; i++)
            {
                if (i > 1)
                    message.Append('\t');
                message.Append(lua.ToString(i, true));
            }

            Log.WriteLine(message.ToString());
            return 0;
        }
        catch (Exception e)
        {
            runtime._hostError ??= e;
            return 0;
        }
    }

    private static LuaRuntime RuntimeOf(LuaState lua) =>
        (LuaRuntime)GCHandle.FromIntPtr(lua.ToUserData(LuaState.UpValueIndex(1))).Target;

    private static int Coordinate(LuaState lua, int index) => (int)Math.Floor(lua.ToNumber(index));

    private static ConsoleColor Color(LuaState lua, int index, ConsoleColor fallback) =>
        lua.Type(index) == LuaType.Number ? (ConsoleColor)(lua.ToInteger(index) & 0xF) : fallback;

    private static char FirstChar(LuaState lua, int index)
    {
        Rune.DecodeFromUtf8(Utf8Bytes(lua, index), out var rune, out _);
        return rune.IsBmp ? (char)rune.Value : '?';
    }

    private static unsafe ReadOnlySpan<byte> Utf8Bytes(LuaState lua, int index)
    {
        var bytes = LuaNative.lua_tolstring(lua.Handle, index, out var length);
        return bytes == IntPtr.Zero ? default : new ReadOnlySpan<byte>((void*)bytes, (int)length);
    }
}
