using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Logging;

namespace ConsoleEngine.Scripting;

/// <summary>
/// Runs the Lua script of every entity with a <see cref="Script"/>: <c>on_start(self)</c> the first time it sees the
/// entity, then <c>on_update(self, dt)</c> every frame. Add it to the simulation group. Add a
/// <see cref="LuaRenderSystem"/> to the presentation group for <c>on_render</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>self</c> mirrors the entity's components: <c>x</c>/<c>y</c> (<see cref="Position"/>), <c>vx</c>/<c>vy</c>
/// (<see cref="Velocity"/>), <c>char</c>/<c>color</c>/<c>background</c> (<see cref="Glyph"/>), plus <c>data</c>, a
/// table the script owns. The fields are copied into <c>self</c> before each hook and back into the components after
/// <c>on_start</c> and <c>on_update</c>. Writing any other field is an error.
/// </para>
/// <para>
/// A Lua error tags the entity <see cref="ScriptFaulted"/> (through <see cref="EntitySystem.Commands"/>), which keeps
/// it out of the script query until its script is reloaded. Its components keep their last good values.
/// </para>
/// <para>
/// The <c>self</c> tables live in a side table indexed by <see cref="Entity.Id"/>, not in the component, because
/// components are unmanaged. A destroyed entity's table is freed when its id is reused.
/// </para>
/// </remarks>
public sealed class LuaScriptSystem(LuaRuntime runtime) : EntitySystem
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private Query _scripts;
    private Query _faulted;
    private Instance[] _instances = new Instance[256];
    private int _seenReloadCount;

    // Stand-ins passed for components an entity doesn't have. RunHook never writes them.
    private Position _noPosition;
    private Velocity _noVelocity;
    private Glyph _noGlyph;

    //**********************************************************
    //** public methods:
    //**********************************************************

    /// <summary>The Lua error that faulted <paramref name="entity"/>'s script, or <c>null</c>.</summary>
    public string GetError(Entity entity)
    {
        if ((uint)entity.Id >= (uint)_instances.Length)
            return null;

        ref var instance = ref _instances[entity.Id];
        return instance.Self > 0 && instance.Version == entity.Version ? instance.Error : null;
    }

    //**********************************************************
    //** lifecycle:
    //**********************************************************

    protected override void OnCreate()
    {
        _scripts = World.Query(Query.With<Script>().Without<ScriptFaulted>());
        _faulted = World.Query(Query.With<Script, ScriptFaulted>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (runtime.ReloadCount != _seenReloadCount)
        {
            _seenReloadCount = runtime.ReloadCount;
            RetryReloadedScripts();
        }

        Run(deltaSeconds, LuaRuntime.ReadBack, Commands);
    }

    //**********************************************************
    //** internals:
    //**********************************************************

    /// <summary>Runs <c>on_render</c> for <see cref="LuaRenderSystem"/>. Field changes are not copied back.</summary>
    internal void Render(float deltaSeconds, CommandBuffer commands) => Run(deltaSeconds, 0, commands);

    /// <summary>One pass over the script query: start and update (<paramref name="readBack"/> set), or render.</summary>
    private void Run(float deltaSeconds, int readBack, CommandBuffer commands)
    {
        foreach (var chunk in _scripts.GetChunks())
        {
            var entities = chunk.Entities;
            var scripts = chunk.GetSpan<Script>();
            var hasPosition = chunk.Has<Position>();
            var hasVelocity = chunk.Has<Velocity>();
            var hasGlyph = chunk.Has<Glyph>();
            var positions = hasPosition ? chunk.GetSpan<Position>() : default;
            var velocities = hasVelocity ? chunk.GetSpan<Velocity>() : default;
            var glyphs = hasGlyph ? chunk.GetSpan<Glyph>() : default;
            var mask = (hasPosition ? LuaRuntime.HasPosition : 0) | (hasVelocity ? LuaRuntime.HasVelocity : 0) |
                       (hasGlyph ? LuaRuntime.HasGlyph : 0);

            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                var script = runtime.GetScript(scripts[i].Id);
                ref var instance = ref GetInstance(entity, script, mask);

                // Faulted entities are tagged and not in this query. An error here means the tag was removed by hand.
                if (instance.Error != null)
                    instance.Error = null;

                ref var position = ref hasPosition ? ref positions[i] : ref _noPosition;
                ref var velocity = ref hasVelocity ? ref velocities[i] : ref _noVelocity;
                ref var glyph = ref hasGlyph ? ref glyphs[i] : ref _noGlyph;

                if (readBack == 0)
                {
                    // Not started means on_update has not run yet: nothing to render from.
                    if (instance.Started && script.OnRender != LuaRuntime.NoRef)
                        RunHook(ref instance, entity, script, script.OnRender, deltaSeconds, mask, commands,
                            ref position, ref velocity, ref glyph);
                    continue;
                }

                if (!instance.Started)
                {
                    // Started only on success, so a reload after a failed on_start runs on_start again.
                    if (script.OnStart != LuaRuntime.NoRef &&
                        !RunHook(ref instance, entity, script, script.OnStart, deltaSeconds, mask | readBack,
                            commands, ref position, ref velocity, ref glyph))
                        continue;
                    instance.Started = true;
                }

                if (script.OnUpdate != LuaRuntime.NoRef)
                    RunHook(ref instance, entity, script, script.OnUpdate, deltaSeconds, mask | readBack, commands,
                        ref position, ref velocity, ref glyph);
            }
        }
    }

    private bool RunHook(ref Instance instance, Entity entity, LuaScript script, int hook, float deltaSeconds,
        int mask, CommandBuffer commands, ref Position position, ref Velocity velocity, ref Glyph glyph)
    {
        var error = runtime.RunHook(hook, instance.Self, deltaSeconds, mask, ref position, ref velocity, ref glyph);
        if (error == null)
            return true;

        instance.Error = error;
        instance.FaultedVersion = script.Version;
        commands.Add<ScriptFaulted>(entity);
        Log.WriteLine($"Lua: {error}");
        return false;
    }

    /// <summary>
    /// The side-table entry for <paramref name="entity"/>, creating its <c>self</c> when the entity is new, its id
    /// was reused, or it now runs another script. Removes the fields of components the entity no longer has.
    /// </summary>
    private ref Instance GetInstance(Entity entity, LuaScript script, int mask)
    {
        if (entity.Id >= _instances.Length)
            Array.Resize(ref _instances, Math.Max(entity.Id + 1, _instances.Length * 2));

        ref var instance = ref _instances[entity.Id];
        if (instance.Self > 0 && (instance.Version != entity.Version || instance.ScriptId != script.Id))
        {
            runtime.Unref(instance.Self);
            instance = default;
        }

        if (instance.Self <= 0)
        {
            instance.Self = runtime.CreateSelf(script);
            instance.Version = entity.Version;
            instance.ScriptId = script.Id;
            instance.Mask = mask;
        }
        else if (instance.Mask != mask)
        {
            var removed = instance.Mask & ~mask & LuaRuntime.ComponentBits;
            if (removed != 0)
                runtime.ClearFields(instance.Self, removed);
            instance.Mask = mask;
        }

        return ref instance;
    }

    /// <summary>Untags faulted entities whose script was reloaded since the fault, so they run again.</summary>
    private void RetryReloadedScripts()
    {
        foreach (var chunk in _faulted.GetChunks())
        {
            var entities = chunk.Entities;
            var scripts = chunk.GetSpan<Script>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                if ((uint)entity.Id >= (uint)_instances.Length)
                    continue;

                ref var instance = ref _instances[entity.Id];
                if (instance.FaultedVersion == runtime.GetScript(scripts[i].Id).Version)
                    continue;

                instance.Error = null;
                Commands.Remove<ScriptFaulted>(entity);
            }
        }
    }

    /// <summary>Lua state for one entity id.</summary>
    private struct Instance
    {
        public int Self;          // registry ref to self, 0 = none
        public int Version;       // Entity.Version the table belongs to
        public int ScriptId;
        public int Mask;          // component bits written into self last time
        public bool Started;
        public int FaultedVersion;
        public string Error;
    }
}
