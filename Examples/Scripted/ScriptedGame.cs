using System;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Infrastructure.Objects;
using ConsoleEngine.Scripting;
using Microsoft.Xna.Framework;

namespace Scripted;

/// <summary>
/// Every game object is an engine <see cref="GameObject"/> whose behaviour is a Lua script in <c>Assets/Scripts</c>.
/// F5 reloads the scripts from the output folder while the game runs.
/// </summary>
public class ScriptedGame() : GameBase(80, 40, 12, 12)
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private const int FireflyCount = 30;

    private readonly Random _random = new(1);
    private LuaRuntime _lua;
    private GameObject[] _objects;

    //**********************************************************
    //** game hooks:
    //**********************************************************

    protected override void OnInitialize()
    {
        _lua = new LuaRuntime(this);
        var player = _lua.Load("Assets/Scripts/player.lua");
        var firefly = _lua.Load("Assets/Scripts/firefly.lua");

        _objects = new GameObject[FireflyCount + 1];
        for (var i = 0; i < FireflyCount; i++)
        {
            _objects[i] = new GameObject
            {
                Position = new Vector2(_random.Next(Console.Width), _random.Next(1, Console.Height - 1)),
                Char = '•',
                Color = ConsoleColor.Green,
                Behaviour = firefly.CreateBehaviour()
            };
        }

        // Last, so the player is drawn on top.
        _objects[FireflyCount] = new GameObject
        {
            Position = Console.ScreenCenter,
            Char = '@',
            Color = ConsoleColor.Yellow,
            Behaviour = player.CreateBehaviour()
        };
    }

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.F5).Pressed)
            ReloadScripts();

        for (var i = 0; i < _objects.Length; i++)
            _objects[i].Update();
    }

    protected override void OnRender()
    {
        for (var i = 0; i < _objects.Length; i++)
            _objects[i].Render(Console);

        // Show the first script error on the bottom line, so a broken script is visible without the logger.
        for (var i = 0; i < _objects.Length; i++)
        {
            if (_objects[i].Behaviour is not LuaBehaviour { IsFaulted: true } behaviour)
                continue;

            Console.Draw(0, Console.Height - 1, behaviour.Error, ConsoleColor.White, ConsoleColor.DarkRed);
            break;
        }
    }

    //**********************************************************
    //** internals:
    //**********************************************************

    private void ReloadScripts()
    {
        foreach (var failure in _lua.ReloadAll())
            Log.WriteLine(failure.Message);
    }
}
