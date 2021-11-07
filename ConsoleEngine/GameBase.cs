using System.Threading;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Logging;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Native;

namespace ConsoleEngine
{
    public abstract class GameBase
    {
        //**********************************************************
        //** fields
        //**********************************************************

        private readonly RenderConsole _console;
        private bool _running;
        
        //**********************************************************
        //** ctor
        //**********************************************************

        protected GameBase(int width, int height, int fontWidth, int fontHeight) 
            : this(width, height, new FontInfo {
                FontFace = "Consolas",
                FontWidth = fontWidth,
                FontHeight = fontHeight
            }) {}
        
        protected GameBase(int width, int height, FontInfo fontInfo) 
        {
            _console = new RenderConsole(new ConsoleHandler(width, height, fontInfo));
            Name = "Game";
        }
              
        //**********************************************************
        //** props
        //**********************************************************

        public RenderConsole Console => _console;
        public string Name { get; init; }
        public bool ShowFps { get; set; }
        public bool EnableLogger { get; init; }
        public bool ClearScreenOnEachFrame { get; set; } = true;
        
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
                Log.Start(this);
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
            while (_running)
            {
                GameTime.Update();
                Input.Instance.Update();
                
                if (ClearScreenOnEachFrame)
                    _console.Clear();
                
                OnUpdate();
                OnRender();
                _console.Display();
            }
        }
        
        public void Stop()
        {
            _running = false;
            _console.Close();
            if (EnableLogger) 
                Log.Stop();
        }
    }
}