using ConsoleEngine.Infrastructure;

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

        protected GameBase(RenderConsole console)
        {
            _console = console;
            Name = "Game";
        }
              
        //**********************************************************
        //** props
        //**********************************************************

        public RenderConsole Console => _console;
        public string Name { get; set; }
        public bool ShowFps { get; set; }
        
        //**********************************************************
        //** abstract methods:
        //**********************************************************

        protected abstract void Update();
        protected abstract void Render();
        
        //**********************************************************
        //** methods:
        //**********************************************************

        public virtual void Initialize()
        {
            _console.Initialize();
        }
        
        public void Start()
        {
            _running = true;
            while (_running)
            {
                GameTimer.Update();
                Input.Instance.Update();
                _console.Clear();
                Update();
                Render();
                if (ShowFps) 
                    _console.SetTitle($"{Name} - {GameTimer.Fps}");
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