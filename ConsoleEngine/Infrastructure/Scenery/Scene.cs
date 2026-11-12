using System;
using ConsoleEngine.Assets;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Infrastructure.Scenery;

public abstract class Scene<TGame> : Scene where TGame : GameBase
{
    public new TGame Game => (TGame)base.Game;
}
    
public abstract class Scene
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private AssetScope _assets;

    //**********************************************************
    //** props:
    //**********************************************************

    public bool IsPaused { get; private set; }
    public GameBase Game { get; internal set; }
    public RenderConsole Console => Game.Console;

    /// <summary>
    /// This scene's assets: loaded through <c>Game.Assets</c>'s cache, owned by the scene, and let go of when the scene
    /// is unloaded (after <see cref="OnUnload"/>). Created on first use, so a scene that loads nothing has none. Use
    /// <c>Game.Assets</c> instead for assets every scene shares.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The scene hasn't been pushed onto a <see cref="SceneManager"/>.
    /// </exception>
    public AssetScope Assets => _assets ??= (AssetManager ?? throw new InvalidOperationException(
        "Scene.Assets is available once the scene is pushed onto the SceneManager (in OnLoad and later).")).CreateScope();

    /// <summary>Set by <see cref="SceneManager.Push(Scene)"/>.</summary>
    internal AssetManager AssetManager { get; set; }
      
    //**********************************************************
    //** abstract:
    //**********************************************************

    public abstract void OnLoad();
    public abstract void OnUnload();
    public abstract void OnUpdate();
    public abstract void OnRender();
              
    //**********************************************************
    //** public methods:
    //**********************************************************

    public void Pause() => IsPaused = true;
    public void Unpause() => IsPaused = false;
              
    //**********************************************************
    //** internals:
    //**********************************************************

    internal void Unload()
    {
        OnUnload();

        // A scene pushed again later gets a fresh scope on first use.
        _assets?.Dispose();
        _assets = null;
        GC.Collect();
    }
        
    internal void Update()
    {
        if (IsPaused) 
            return;

        OnUpdate();
    }

    internal void Render()
    {
        if (IsPaused) 
            return;

        OnRender();
    }
}