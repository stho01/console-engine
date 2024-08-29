using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Infrastructure.Scenery;

public abstract class Scene<TGame> : Scene where TGame : GameBase
{
    public new TGame Game => (TGame)base.Game;
}
    
public abstract class Scene
{
    //**********************************************************
    //** props:
    //**********************************************************

    public bool IsPaused { get; private set; }
    public GameBase Game { get; internal set; }
    public RenderConsole Console => Game.Console;
      
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