using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;

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

        protected GameBase(RenderConsole console) : this(console, "Game") {}
        protected GameBase(RenderConsole console, string name)
        {
            _console = console;
            Name = name;
        }
              
        //**********************************************************
        //** props
        //**********************************************************

        public RenderConsole Console => _console;
        public string Name { get; set; }
        public bool ShowFps { get; set; }
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
            OnInitialize();
            _console.SetTitle(Name);
            _console.Initialize();
        }
        
        public void Start()
        {
            _running = true;
            while (_running)
            {
                GameTime.Update();
                Input.Instance.Update();
                
                if (ClearScreenOnEachFrame)
                    _console.Clear();
                
                OnUpdate();
                OnRender();
                
                if (ShowFps) 
                    _console.SetTitle($"{Name} - {GameTime.Fps}");
                
                _console.Display();
            }
        }
        
        public void Stop()
        {
            _running = false;
            _console.Close();
        }
    }
}