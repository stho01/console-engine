using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Infrastructure.Objects;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Scripting.LowLevel;
using KeraLua;
using Microsoft.Xna.Framework;
using LuaState = KeraLua.Lua;

namespace ConsoleEngine.Scripting;

/// <summary>
/// One native Lua 5.4 state with the engine API registered: <c>input</c>, <c>console</c>, <c>keys</c>, <c>colors</c>
/// and <c>print</c> (to <see cref="Log"/>). Load scripts with <see cref="Load"/> and attach them to game objects with
/// <see cref="LuaScript.CreateBehaviour"/>. Not thread-safe: use it from the game-loop thread only.
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

    private const int KeyCount = 256; // size of the key tables in InputHandler
    private const int MaxTextLength = 256;

    // Static so native code always points to a live delegate. Each closure finds its runtime through upvalue 1.
    private static readonly LuaFunction HeldFunction = Held;
    private static readonly LuaFunction PressedFunction = Pressed;
    private static readonly LuaFunction ReleasedFunction = Released;
    private static readonly LuaFunction DrawFunction = Draw;
    private static readonly LuaFunction TextFunction = Text;
    private static readonly LuaFunction PrintFunction = Print;

    // Raised by __newindex on a game object table: only the engine fields below exist, script data goes in self.data.
    private const string UnknownFieldGuardSource =
        "return function(self, key) error(\"unknown field '\" .. tostring(key) .. \"' on a game object, " +
        "put script data in self.data\", 2) end";

    private readonly LuaState _lua;
    private readonly GCHandle _handle;
    private readonly RenderConsole _console;
    private readonly Func<int, KeyState> _getKey;
    private readonly List<LuaScript> _scripts = new();
    private Exception _hostError;

    // Registry refs to the engine field names, so syncing a GameObject never marshals a .NET string.
    private readonly int _keyX;
    private readonly int _keyY;
    private readonly int _keyChar;
    private readonly int _keyColor;
    private readonly int _keyBackground;
    private readonly int _keyVisible;
    private readonly int _keyData;

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

        _keyX = StringRef("x");
        _keyY = StringRef("y");
        _keyChar = StringRef("char");
        _keyColor = StringRef("color");
        _keyBackground = StringRef("background");
        _keyVisible = StringRef("visible");
        _keyData = StringRef("data");

        _lua.LoadString(UnknownFieldGuardSource, "=engine");
        Call(0, 1);
        UnknownFieldGuard = Ref();
    }

    //**********************************************************
    //** props:
    //**********************************************************

    internal LuaState State => _lua;

    /// <summary>Registry ref to the <c>__newindex</c> function of every game object table.</summary>
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
        var script = new LuaScript(this, path, fullPath);
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

    internal void Push(int reference) => _lua.RawGetInteger(LuaRegistry.Index, reference);

    /// <summary>Pops the top value into the registry and returns its reference.</summary>
    internal int Ref() => _lua.Ref(LuaRegistry.Index);

    internal void Unref(int reference)
    {
        if (reference != NoRef)
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

    /// <summary>Pushes a new game object table: <c>{ data = {} }</c>. The engine fields are written before each hook.
    /// </summary>
    internal void PushNewSelf()
    {
        _lua.CreateTable(0, 7);
        Push(_keyData);
        _lua.NewTable();
        _lua.RawSet(-3);
    }

    /// <summary>Copies the engine fields of <paramref name="owner"/> into the Lua table <paramref name="self"/>.</summary>
    internal void WriteFields(int self, GameObject owner)
    {
        Push(self);
        SetNumber(_keyX, owner.Position.X);
        SetNumber(_keyY, owner.Position.Y);
        SetNumber(_keyColor, (int)owner.Color);
        SetNumber(_keyBackground, (int)owner.Background);

        Push(_keyVisible);
        _lua.PushBoolean(owner.Visible);
        _lua.RawSet(-3);

        Push(_keyChar);
        PushChar(owner.Char);
        _lua.RawSet(-3);

        _lua.Pop(1);
    }

    /// <summary>Copies the engine fields back from <paramref name="self"/> into <paramref name="owner"/>.</summary>
    /// <returns><c>null</c>, or the error when a script stored a value of the wrong type (nothing is copied then).
    /// </returns>
    internal string ReadFields(int self, GameObject owner)
    {
        Push(self);
        try
        {
            if (!TryGetNumber(_keyX, out var x)) return "self.x must be a number";
            if (!TryGetNumber(_keyY, out var y)) return "self.y must be a number";
            if (!TryGetNumber(_keyColor, out var color)) return "self.color must be a number";
            if (!TryGetNumber(_keyBackground, out var background)) return "self.background must be a number";
            if (!TryGetChar(out var character)) return "self.char must be a string or a code point";

            Push(_keyVisible);
            _lua.RawGet(-2);
            var visible = _lua.ToBoolean(-1);
            _lua.Pop(1);

            owner.Position = new Vector2((float)x, (float)y);
            owner.Color = (ConsoleColor)((int)color & 0xF);
            owner.Background = (ConsoleColor)((int)background & 0xF);
            owner.Char = character;
            owner.Visible = visible;
            return null;
        }
        finally
        {
            _lua.Pop(1);
        }
    }

    /// <summary>Stack: [table] → [table]. Sets table[key] without metamethods.</summary>
    private void SetNumber(int key, double value)
    {
        Push(key);
        _lua.PushNumber(value);
        _lua.RawSet(-3);
    }

    /// <summary>Stack: [table] → [table].</summary>
    private bool TryGetNumber(int key, out double value)
    {
        Push(key);
        _lua.RawGet(-2);
        var isNumber = _lua.Type(-1) == LuaType.Number;
        value = isNumber ? _lua.ToNumber(-1) : 0;
        _lua.Pop(1);
        return isNumber;
    }

    /// <summary>Stack: [table] → [table]. Reads <c>char</c> as a string (first character) or a code point.</summary>
    private bool TryGetChar(out char value)
    {
        Push(_keyChar);
        _lua.RawGet(-2);
        var type = _lua.Type(-1);
        value = type switch
        {
            LuaType.String => FirstChar(_lua, -1),
            LuaType.Number => (char)_lua.ToInteger(-1),
            _ => '\0'
        };
        _lua.Pop(1);
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

    private int StringRef(string value)
    {
        _lua.PushString(value);
        return Ref();
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
