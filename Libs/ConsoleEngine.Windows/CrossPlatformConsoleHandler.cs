using System;
using System.Runtime.InteropServices;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Native.LowLevel;

namespace ConsoleEngine.Native;

using static Kernel32;
using static User32;
    
public sealed class CrossPlatformConsoleHandler : IConsoleHandler
{
    private readonly FontInfo _fontInfo;
    public CrossPlatformConsoleHandler(int width, int height, FontInfo fontInfo)
    {
        _fontInfo = fontInfo;
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public void InitializeConsole()
    {
        Console.Clear();
        SetFont(_fontInfo);
    }

    public void SetTitle(string title)
    {
        Console.Title = title;
    }

    public void SetCursorVisible(bool visible)
    {
        try { Console.CursorVisible = visible; } catch { }
    }

    public void Resizable(bool resizable)
    {
        // Not yet supported
    }

    public void SetFont(FontInfo fontInfo)
    {
        // Not yet supported  
    }

    public void Render(Span<Pixel> pixels)
    {
        Console.SetCursorPosition(0, 0);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                if (i < pixels.Length)
                    Console.Write(pixels[i].Char);
                else
                    Console.Write(' ');
            }
            Console.WriteLine();
        }
    }

    public void Close()
    {
        // Not yet supported 
    }

    private static ushort GetColorValue(ConsoleColor fg, ConsoleColor bg) => (ushort)((int)fg | ((int)bg<<4));
}