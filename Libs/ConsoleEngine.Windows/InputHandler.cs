using ConsoleEngine.Abstractions.Inputs;

namespace ConsoleEngine.Native
{
    using static LowLevel.Kernel32;
    
    public class InputHandler : IInputHandler
    {
        private readonly int[] _previousState;
        private readonly int[] _newState;
        private readonly KeyState[] _keyStates;

        public InputHandler()
        {
            _previousState = new int[256];
            _newState = new int[256];
            _keyStates = new KeyState[256];
        }
        
        public KeyState GetKey(int id)
        {
            return _keyStates[id];
        }

        public void Update()
        {
            for (var i = 0; i < 256; i++)
            {
                _newState[i] = GetAsyncKeyState(i);
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