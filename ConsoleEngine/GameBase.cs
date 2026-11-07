using System;
using System.Threading;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Infrastructure.Scenery;

namespace ConsoleEngine;

public abstract class GameBase
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly RenderConsole _console;
    private volatile bool _running;
    private int _cancelRequests;

    //**********************************************************
    //** ctor
    //**********************************************************

    protected GameBase(int width, int height, int fontWidth, int fontHeight)
        : this(width, height, new FontInfo
        {
            FontFace = "Consolas",
            FontWidth = fontWidth,
            FontHeight = fontHeight
        }) {}
        
    protected GameBase(int width, int height, FontInfo fontInfo) 
    {
        _console = new RenderConsole(Platform.CreateConsoleHandler(width, height, fontInfo));
        Name = "Game";
        Scenes = new SceneManager(this);
    }
              
    //**********************************************************
    //** props
    //**********************************************************

    public RenderConsole Console => _console;
    public string Name { get; init; }
    public bool ShowFps { get; set; }
    public bool EnableLogger { get; init; }
    public bool ClearScreenOnEachFrame { get; set; } = true;
    public SceneManager Scenes { get; }
    public Scene Scene => Scenes.Current;

    //**********************************************************
    //** abstract methods:
    //**********************************************************

    protected abstract void OnInitialize();
    protected abstract void OnUpdate();
    protected abstract void OnRender();
        
    //**********************************************************
    //** methods:
    //**********************************************************

    public void Initialize()
    {
        if (EnableLogger) {
            Log.Start();
        }

        OnInitialize();
        _console.SetTitle(Name);
        _console.Initialize();

        if (ShowFps)
            GameTime.SetInterval(100, () => Log.ReportFps(GameTime.Fps));
    }
        
    public void Start()
    {
        Thread.Sleep(100); // lazy way to wait for the console to load and initialize xD. 
            
        _running = true;
        System.Console.CancelKeyPress += OnCancelKeyPress;
        try
        {
            while (_running)
            {
                GameTime.Update();
                Input.Instance.Update();

                if (ClearScreenOnEachFrame)
                    _console.Clear();

                Scenes.Update();
                OnUpdate();

                Scenes.Render();
                OnRender();

                _console.Display();

                Scenes.Cleanup();
            }
        }
        catch
        {
            // Give the terminal back before the exception is printed, otherwise it ends up in the alternate screen.
            _console.Restore();
            throw;
        }
        finally
        {
            System.Console.CancelKeyPress -= OnCancelKeyPress;
        }

        // Stopped by Ctrl+C
        if (EnableLogger)
            Log.Stop();
    }
        
    /// <summary>
    /// The first Ctrl+C lets the current frame finish and returns from <see cref="Start"/>, the second one
    /// terminates the process right away in case the game hangs.
    /// </summary>
    private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        if (Interlocked.Increment(ref _cancelRequests) > 1)
            return;

        e.Cancel = true;
        _running = false;
        _console.Restore();
        System.Console.WriteLine($"Shutting down {Name}... press Ctrl+C again to force quit.");
    }

    public void Stop()
    {
        _running = false;
        _console.Close();
        if (EnableLogger) 
            Log.Stop();
    }

        
}