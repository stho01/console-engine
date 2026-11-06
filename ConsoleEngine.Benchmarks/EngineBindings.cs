using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Benchmarks;

/// <summary>
/// What a benchmark frame needs from the engine without Win32: a real <see cref="RenderConsole"/> over
/// <see cref="NullConsoleHandler"/>, and a fake key table because <c>Input.Instance</c> needs Win32.
/// </summary>
public class EngineBindings
{
    //**********************************************************
    //** fields:
    //**********************************************************

    public const int ScreenWidth = 200;
    public const int ScreenHeight = 200;
    public const double FrameDelta = 1.0 / 60.0;

    private const int KeyLeft = 0x25;
    private const int KeyRight = 0x27;

    private readonly bool[] _keys = new bool[256];

    //**********************************************************
    //** props:
    //**********************************************************

    public RenderConsole Console { get; } = new(new NullConsoleHandler(ScreenWidth, ScreenHeight));

    //**********************************************************
    //** public methods:
    //**********************************************************

    /// <summary>Deterministic key pattern, so every run sees the same input on the same frame.</summary>
    public void BeginFrame(int frame)
    {
        _keys[KeyLeft] = frame % 4 == 0;
        _keys[KeyRight] = frame % 3 == 0;
    }

    public bool IsDown(int key) => _keys[key];

    public KeyState GetKey(int key) => new() { Held = _keys[key] };
}
