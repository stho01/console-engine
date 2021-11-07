using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Native;

namespace ConsoleEngine.Infrastructure.Inputs
{
    public class Input
    {
        //**********************************************************
        //** fields:
        //**********************************************************

        public static readonly Input Instance = new();
        private static readonly IInputHandler _handler = new InputHandler();
        
        //**********************************************************
        //** ctor:
        //**********************************************************

        private Input() { }
              
        //**********************************************************
        //** public methods:
        //**********************************************************
        
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