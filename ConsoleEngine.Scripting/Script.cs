namespace ConsoleEngine.Scripting;

/// <summary>
/// Runs a Lua script on this entity. Get it from <see cref="LuaScript.Component"/>. The script sees the entity's
/// <c>Position</c>, <c>Velocity</c> and <c>Glyph</c> as fields of <c>self</c>. <see cref="LuaScriptSystem"/> runs it.
/// </summary>
public readonly struct Script(int id)
{
    /// <summary><see cref="LuaScript.Id"/> of the script.</summary>
    public readonly int Id = id;
}

/// <summary>
/// Tag: the entity's script raised an error, so <see cref="LuaScriptSystem"/> skips it until the script is reloaded.
/// The error text is <see cref="LuaScriptSystem.GetError"/>.
/// </summary>
public struct ScriptFaulted;
