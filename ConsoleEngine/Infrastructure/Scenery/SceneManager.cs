using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleEngine.Infrastructure.Scenery;

public class SceneManager
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private readonly GameBase _game;
    private readonly Stack<Scene> _scenes = new();
    private readonly Queue<Scene> _unloadScenes = new();

    //**********************************************************
    //** ctor:
    //**********************************************************

    internal SceneManager(GameBase game)
    {
        _game = game;
    }
              
    //**********************************************************
    //** props
    //**********************************************************

    public int Count => _scenes.Count;
        
    public Scene Current
    {
        get
        {
            _scenes.TryPeek(out var top);
            return top;
        }
    }

    public Scene PreviousScene
    {
        get
        {
            var current = _scenes.Pop();
            _scenes.TryPeek(out var top);
            _scenes.Push(current);
            return top;
        }
    }
        
    //**********************************************************
    //** public methods:
    //**********************************************************
 
    /// <summary>Unloads current stack and sets the new scene as the current one</summary>
    /// <typeparam name="T">The new scene</typeparam>
    public T Set<T>() where T : Scene, new()
    {
        var scene = new T();
            
        Set(scene);

        return scene;
    }
        
    /// <summary>Unloads current stack and sets the new scene as the current one</summary>
    /// <param name="scene">The new scene</param>
    public void Set(Scene scene)
    {
        while (_scenes.Any())
            _unloadScenes.Enqueue(_scenes.Pop());

        Push(scene);
    }

    /// <summary>
    /// Push and load a scene on top of the stack, pausing the currently active
    /// scene if any.  
    /// </summary>
    /// <typeparam name="T">The scene type that should be instantiated and loaded</typeparam>
    /// <returns>The instantiated and loaded scene</returns>
    public T Push<T>() where T : Scene, new() 
    {
        var scene = new T { Game = _game };
            
        Push(scene);
            
        return scene;
    }
        
    /// <summary>
    /// Push and load a scene on top of the stack, pausing the currently active
    /// scene if any.  
    /// </summary>
    /// <param name="scene">The scene type that should be loaded</param>
    public void Push(Scene scene)
    {
        if (_scenes.TryPeek(out var current))
            current.Pause();

        scene.Game = _game;
        scene.OnLoad();
        _scenes.Push(scene);
    }
        
    /// <summary>
    /// Pops and unloads the current scene off the stack. Unpauses the next scene in the stack if any.
    /// </summary>
    /// <returns>The scene that was popped from stack</returns>
    public Scene Pop()
    {
        if (!_scenes.TryPop(out var old))
            return null;
            
        _unloadScenes.Enqueue(old);
            
        if (_scenes.TryPeek(out var @new))
            @new.Unpause();
            
        return old;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    internal void Update()
    {
        if (_scenes.TryPeek(out var current))
            current.Update();
    }

    internal void Render()
    {
        if (_scenes.TryPeek(out var current))
            current.Render();
    }

    internal void Cleanup()
    {
        if (!_unloadScenes.Any()) 
            return;
            
        while (_unloadScenes.Any())
            _unloadScenes.Dequeue().Unload();
            
        GC.Collect();
    }
}