using System;

namespace ConsoleEngine.Scripting;

/// <summary>
/// A script could not be loaded: the file is missing, has a syntax or top-level runtime error, or returns no table.
/// </summary>
public class LuaScriptException(string path, string message) : Exception($"Lua script '{path}': {message}")
{
    public string ScriptPath { get; } = path;
}
