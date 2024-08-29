﻿using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;

namespace ConsoleEngine.Native;

using static LowLevel.Kernel32;
    
public class InputHandler : IInputHandler
{
    private readonly int[] _previousState = new int[256];
    private readonly int[] _newState = new int[256];
    private readonly KeyState[] _keyStates = new KeyState[256];

    public KeyState GetKey(int id)
    {
        return _keyStates[id];
    }

    public IEnumerable<int> GetPressedKeyCodes() => _keyStates.Where(k => k.Pressed).Select(k => k.Index);

    public void Update()
    {
        for (var i = 0; i < 256; i++)
        {
            _newState[i] = GetAsyncKeyState(i);
            _keyStates[i].Pressed = false;
            _keyStates[i].Released = false;
            _keyStates[i].Index = i;

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