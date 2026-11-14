using System;
using System.Runtime.InteropServices;

namespace ConsoleEngine.Scripting.LowLevel;

/// <summary>
/// Lua C API functions that KeraLua only exposes through allocating wrappers. <c>lua54</c> is the native library that
/// the KeraLua package ships for each runtime.
/// </summary>
internal static unsafe class LuaNative
{
    private const string LuaLibrary = "lua54";

    /// <summary>Pointer to the UTF-8 bytes of the string at <paramref name="index"/>, without creating a .NET string.
    /// Converts a number at that index to a string in place.</summary>
    [DllImport(LuaLibrary, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lua_tolstring(IntPtr luaState, int index, out UIntPtr length);

    /// <summary>Pushes a string from UTF-8 bytes, without creating a .NET string.</summary>
    [DllImport(LuaLibrary, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lua_pushlstring(IntPtr luaState, byte* bytes, UIntPtr length);
}
