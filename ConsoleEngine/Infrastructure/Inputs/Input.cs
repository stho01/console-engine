using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Native;

namespace ConsoleEngine.Infrastructure.Inputs;

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

    public IEnumerable<int> GetPressedKeyCodesSpace09AZ() => GetPressedKeyCodes().Where(c => c == 32 || (c >= 48 && c <= 90));
        
    private IEnumerable<int> GetPressedKeyCodes() => _handler?.GetPressedKeyCodes();
        
    internal void Update()
    {
        _handler?.Update();
    }
}