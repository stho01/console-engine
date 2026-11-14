using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Scripting;

namespace Scripted.Systems;

/// <summary>F5 reloads every script. A script that fails to reload keeps running its previous version.</summary>
public sealed class ScriptReloadSystem(LuaRuntime lua) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        if (!Input.Instance.GetKey(Key.F5).Pressed)
            return;

        foreach (var failure in lua.ReloadAll())
            Log.WriteLine(failure.Message);
    }
}
