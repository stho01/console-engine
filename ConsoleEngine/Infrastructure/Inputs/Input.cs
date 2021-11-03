using System;

namespace ConsoleEngine.Infrastructure.Inputs
{
    public class Input
    {
        //**********************************************************
        //** fields:
        //**********************************************************

        public static readonly Input Instance = new();
        private IInputHandler _handler;
        
        //**********************************************************
        //** ctor:
        //**********************************************************

        private Input() { }
              
        //**********************************************************
        //** public methods:
        //**********************************************************

        public void SetHandler(IInputHandler handler)
        {
            _handler = handler;
        }
        
        public KeyState GetKey(Key key) => GetKey((int)key);
        public KeyState GetKey(int id) 
        {
            return _handler?.GetKey(id) ?? throw new InvalidOperationException("Input handler not set");
        }

        internal void Update()
        {
            _handler?.Update();
        }
    }
}