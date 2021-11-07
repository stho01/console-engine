using System;
using System.Runtime.InteropServices;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Native.LowLevel;

namespace ConsoleEngine.Native
{
    using static LowLevel.Kernel32;
    using static LowLevel.User32;
    
    public sealed class ConsoleHandler : IConsoleHandler
    {
        private readonly FontInfo _fontInfo;
        private readonly IntPtr _consoleOutBuffer;
        private readonly CharInfo[] _chars;
        private SmallRect _writeRegion;
        
        public ConsoleHandler(int width, int height, FontInfo fontInfo)
        {
            _fontInfo = fontInfo;
            Width = width;
            Height = height;
            _chars = new CharInfo[width*height];
            _writeRegion = new SmallRect(0, 0, (short)Width, (short)Height);
            _consoleOutBuffer = GetStdHandle(StdHandle.OutputHandle);
        }

        public int Width { get; }
        public int Height { get; }

        public void InitializeConsole()
        {
            var initSize = new SmallRect(0, 0, 1, 1);
            SetConsoleWindowInfo(_consoleOutBuffer, true, ref initSize);
            
            var size = new Coord((short)Width, (short)Height);
            
            SetConsoleScreenBufferSize(_consoleOutBuffer, size);
            
            SetConsoleActiveScreenBuffer(_consoleOutBuffer);
          
            SetFont(_fontInfo);
            
            var windowRect = new SmallRect(0, 0, (short)(Width-1), (short)(Height-1));
            SetConsoleWindowInfo(_consoleOutBuffer, true, ref windowRect);
        }

        public void SetTitle(string title) => SetConsoleTitle(title.ToCharArray());

        public void SetCursorVisible(bool visible)
        {
            GetConsoleCursorInfo(_consoleOutBuffer, out var cursorInfo);
            cursorInfo.Visible = visible;
            SetConsoleCursorInfo(_consoleOutBuffer, ref cursorInfo);
        }

        public void Resizable(bool resizable)
        {
            // TODO: toggle between true and false. 
            // as of now this is disable only.
            if (resizable == false)
            {
                var handle = GetConsoleWindow();

                if (handle == IntPtr.Zero) 
                    return;
            
                var sysMenu = GetSystemMenu(handle, false);
                DeleteMenu(sysMenu, SC_MAXIMIZE, MF_BYCOMMAND);
                DeleteMenu(sysMenu, SC_SIZE, MF_BYCOMMAND);
            }
        }

        public void SetFont(FontInfo fontInfo)
        {
            var font = new ConsoleFontInfoEx();
            font.cbSize = (uint)Marshal.SizeOf(font);
            font.FaceName = fontInfo.FontFace;
            font.dwFontSize.X = (short)fontInfo.FontWidth;
            font.dwFontSize.Y = (short)fontInfo.FontHeight;
            SetCurrentConsoleFontEx(_consoleOutBuffer, false, ref font);
        }

        public void Render(Span<Pixel> pixels)
        {
            var resolution = new Coord((short)Width, (short)Height);

            for (var i = 0; i < pixels.Length; i++)
            {
                _chars[i].UnicodeChar = pixels[i].Char;
                _chars[i].Attributes = GetColorValue(pixels[i].ForegroundColor, pixels[i].BackgroundColor);
            }
            
            WriteConsoleOutput(_consoleOutBuffer, _chars, resolution, Coord.Zero, ref _writeRegion);
        }

        public void Close()
        {
            // close console (?)
        }

        private static ushort GetColorValue(ConsoleColor fg, ConsoleColor bg) => (ushort)((int)fg | ((int)bg<<4));
    }
}