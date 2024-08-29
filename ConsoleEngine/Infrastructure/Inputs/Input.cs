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
    private static readonly IInputHandler Handler = new InputHandler();
        
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
        return Handler?.GetKey(id) ?? throw new InvalidOperationException("Input handler not set");
    }

    public IEnumerable<int> GetPressedKeyCodesSpace09AZ() => GetPressedKeyCodes().Where(c => c is 32 or >= 48 and <= 90);
        
    private IEnumerable<int> GetPressedKeyCodes() => Handler?.GetPressedKeyCodes();
        
    internal void Update() => Handler?.Update();
}