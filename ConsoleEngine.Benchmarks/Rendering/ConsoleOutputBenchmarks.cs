using System;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Native;

namespace ConsoleEngine.Benchmarks.Rendering;

/// <summary>
/// The managed part of putting a 320×180 frame (Kartster's size) on screen: a game copying its colours in with
/// <see cref="RenderConsole.Draw(int, int, char, ConsoleColor, ConsoleColor)"/> per cell, and
/// <see cref="ConsoleHandler"/>'s row diff against the last frame. The <c>WriteConsoleOutput</c> call itself only
/// runs in a real console, see AGENTS.md § Performance for its measured cost.
/// </summary>
[MemoryDiagnoser]
public class ConsoleOutputBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int Width = 320;
    private const int Height = 180;

    private RenderConsole _console;
    private ConsoleColor[] _colors;
    private uint[] _shown;
    private uint[] _unchanged;
    private uint[] _allChanged;

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup]
    public void Setup()
    {
        _console = new RenderConsole(new NullConsoleHandler(Width, Height));
        _colors = new ConsoleColor[Width * Height];
        _shown = new uint[Width * Height];
        _unchanged = new uint[Width * Height];
        _allChanged = new uint[Width * Height];
        for (var i = 0; i < _colors.Length; i++)
        {
            _colors[i] = (ConsoleColor)(i % 16);
            _shown[i] = _unchanged[i] = (uint)(i % 16) << 20;
            _allChanged[i] = _shown[i] + 1;
        }
    }

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark]
    public void DrawPerCell()
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            _console.Draw(x, y, ' ', ConsoleColor.Black, _colors[y * Width + x]);
    }

    /// <summary>Worst case for the diff: it scans the whole frame and writes nothing.</summary>
    [Benchmark]
    public bool FindChangedRows_Unchanged() => ConsoleHandler.FindChangedRows(_unchanged, _shown, Width, out _, out _);

    [Benchmark]
    public bool FindChangedRows_AllChanged() => ConsoleHandler.FindChangedRows(_allChanged, _shown, Width, out _, out _);

    //**********************************************************
    //** internals
    //**********************************************************

    private sealed class NullConsoleHandler(int width, int height) : IConsoleHandler
    {
        public int Width { get; } = width;
        public int Height { get; } = height;

        public void InitializeConsole() { }
        public void SetTitle(string title) { }
        public void SetCursorVisible(bool visible) { }
        public void Resizable(bool resizable) { }
        public void SetFont(FontInfo fontInfo) { }
        public void Render(Span<Pixel> pixels) { }
        public void Close() { }
    }
}
