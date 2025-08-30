using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;

namespace ConsoleEngine.Native;

using static LowLevel.Kernel32;
    
public class CrossPlatformInputHandler : IInputHandler
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
        bool[] pressedThisFrame = new bool[_keyStates.Length];
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(true);
            int key = (int)keyInfo.Key;
            _keyStates[key].Pressed = true;
            _keyStates[key].Held = true;
            _keyStates[key].Index = key;
            pressedThisFrame[key] = true;
        }
        for (int i = 0; i < _keyStates.Length; i++)
        {
            if (!pressedThisFrame[i])
            {
                if (_keyStates[i].Held)
                {
                    _keyStates[i].Released = true;
                }
                else
                {
                    _keyStates[i].Released = false;
                }
                _keyStates[i].Held = false;
                _keyStates[i].Pressed = false;
            }
        }
    }
}