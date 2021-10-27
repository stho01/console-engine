using ConsoleEngine.LowLevel;

namespace ConsoleEngine.Infrastructure
{
    public class Input
    {
        public static readonly Input Instance = new(); 
        
        //**********************************************************
        //** fields:
        //**********************************************************

        private readonly int[] _previousState;
        private readonly int[] _newState;
        private readonly KeyState[] _keyStates;
        
        //**********************************************************
        //** ctor:
        //**********************************************************

        private Input()
        {
            _previousState = new int[256];
            _newState = new int[256];
            _keyStates = new KeyState[256];
        }
              
        //**********************************************************
        //** public methods:
        //**********************************************************

        public KeyState GetKey(Key key) => GetKey((int)key);
        
        public KeyState GetKey(int id)
        {
            return _keyStates[id];
        }

        internal void Update()
        {
            for (var i = 0; i < 256; i++)
            {
                _newState[i] = Kernel32.GetAsyncKeyState(i);
                _keyStates[i].Pressed = false;
                _keyStates[i].Released = false;

                if (_newState[i] != _previousState[i])
                {
                    if ((_newState[i] & 0x8000) != 0)
                    {
                        _keyStates[i].Pressed = !_keyStates[i].Held;
                        _keyStates[i].Held = true;
                    }
                    else
                    {
                        _keyStates[i].Released = true;
                        _keyStates[i].Held = false;
                    }
                }

                _previousState[i] = _newState[i];
            }
        }
    }
}