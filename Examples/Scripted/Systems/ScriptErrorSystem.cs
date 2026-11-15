using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Scripting;

namespace Scripted.Systems;

/// <summary>Shows the first script error on the bottom line, so a broken script is visible without the logger.</summary>
public sealed class ScriptErrorSystem(RenderConsole console, LuaScriptSystem scripts) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _faulted;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _faulted = World.Query(Query.With<ScriptFaulted>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _faulted.GetChunks())
        {
            if (chunk.Count == 0)
                continue;

            var error = scripts.GetError(chunk.Entities[0]);
            if (error != null)
                console.Draw(0, console.Height - 1, error, ConsoleColor.White, ConsoleColor.DarkRed);
            return;
        }
    }
}
