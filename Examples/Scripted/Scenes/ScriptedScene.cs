using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Scripting;
using Microsoft.Xna.Framework;
using Scripted.Systems;

namespace Scripted.Scenes;

/// <summary>
/// A player and 30 fireflies. Both are scripted in Lua, but the engine still does the generic work: fireflies get a
/// <see cref="Velocity"/> that <see cref="MovementSystem"/> applies, and <see cref="GlyphRenderSystem"/> draws
/// every <see cref="Glyph"/>. The scripts only steer, bounce and recolour.
/// </summary>
public sealed class ScriptedScene : EcsScene<ScriptedGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int FireflyCount = 30;

    private LuaRuntime _lua;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _lua = new LuaRuntime(Game);
        var player = _lua.Load("Assets/Scripts/player.lua");
        var firefly = _lua.Load("Assets/Scripts/firefly.lua");

        var random = new Random(1);
        for (var i = 0; i < FireflyCount; i++)
        {
            var position = new Vector2(random.Next(Console.Width), random.Next(1, Console.Height - 1));
            World.Create(new Position(position), new Velocity(), new Glyph('•', ConsoleColor.Green),
                firefly.Component);
        }

        World.Create(new Position(Console.ScreenCenter), new Glyph('@', ConsoleColor.Yellow), player.Component);

        // Scripts steer first, then the engine moves.
        var scripts = Simulation.Add(new LuaScriptSystem(_lua));
        Simulation.Add(new MovementSystem());
        Simulation.Add(new ScriptReloadSystem(_lua));

        Presentation.Add(new GlyphRenderSystem(Console, Camera));
        Presentation.Add(new LuaRenderSystem(scripts));
        Presentation.Add(new ScriptErrorSystem(Console, scripts));
    }

    protected override void OnDestroy() => _lua.Dispose();
}
