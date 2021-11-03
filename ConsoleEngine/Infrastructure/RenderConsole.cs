using System;
using System.Data;
using System.Runtime.InteropServices;
using ConsoleEngine.Extensions;
using ConsoleEngine.LowLevel;
using Microsoft.Toolkit.HighPerformance;

namespace ConsoleEngine.Infrastructure
{
    using static LowLevel.Kernel32;
    using static LowLevel.User32;
    
    public class RenderConsole
    {
        //**********************************************************
        //** fields:
        //**********************************************************

        private readonly IntPtr _consoleOutBuffer;
        private readonly CharInfo[] _chars;
        private SmallRect _writeRegion;
        private bool _cursorVisible;
        
        //**********************************************************
        //** ctor:
        //**********************************************************

        public RenderConsole(int width, int height) 
        {
            Width = width;
            Height = height;
            Area = Width * height;
            FontWidth = 14;
            FontHeight = 14;
            HideCursor = true;
            _chars = new CharInfo[width*height];
            _writeRegion = new SmallRect(0, 0, (short)Width, (short)Height);
            _consoleOutBuffer = GetStdHandle(StdHandle.OutputHandle);
        }
          
        //**********************************************************
        //** props:
        //**********************************************************

        public int Width { get; }
        public int Height { get; }
        /// <summary> The total Screen area. W * H </summary>
        public int Area { get; }
        public int FontWidth { get; init; }
        public int FontHeight { get; init; }
        public bool Resizeable { get; init; }
        public bool HideCursor 
        {
            get => _cursorVisible;
            init => _cursorVisible = !value;
        }
        public string FontFace { get; init; } = "Consolas";

        //**********************************************************
        //** public methods:
        //**********************************************************

        internal RenderConsole Initialize()
        {
            InitBuffer(_consoleOutBuffer);
            
            if (!Resizeable) 
                DisableResize();

            SetCursorVisible(_cursorVisible);
            
            return this;
        }

        public void Draw(int x, int y, Sprite sprite)
        {
            for (var dataX = 0; dataX < sprite.DataSpan.Width; dataX++)
            for (var dataY = 0; dataY < sprite.DataSpan.Height; dataY++) {
                var pixel = sprite.DataSpan[dataX, dataY];
                Draw(dataX + x, dataY + y, pixel.Char, pixel.ForegroundColor, pixel.BackgroundColor);
            }
        }

        public void Draw(int x, int y, Span2D<char> data)
        {
            //var d = data.Transpose(); // TODO: Transpose (?)
            for (var dataX = 0; dataX < data.Width; dataX++)
            for (var dataY = 0; dataY < data.Height; dataY++) {
                Draw(dataX + x, dataY + y, data[dataX, dataY]);
            }
        }
        
        public void Draw(int x, int y, short charCode) => Draw(x, y, (char)charCode);
        
        public void Draw(int x, int y, char character, ConsoleColor foregroundColor = ConsoleColor.White, ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                var index = y * Width + x;
                _chars[index].UnicodeChar = character;
                _chars[index].Attributes = GetColorValue(foregroundColor, backgroundColor);
            }
        }

        public void Draw(int x, int y, Span<string> rows)
        {
            for (var dataY = 0; dataY < rows.Length; dataY++)
            for (var dataX = 0; dataX < rows[dataY].Length; dataX++)
            {
                var index = (dataY + y) * Width + (dataX + x);
                _chars[index].UnicodeChar = rows[dataY][dataX];
                _chars[index].Attributes = GetColorValue(ConsoleColor.Green, ConsoleColor.Black);
            }
        }
        
        public char? GetCharAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return null;
            
            return _chars[y * Width + x].UnicodeChar;
        }

        internal void Display()
        {
            var resolution = new Coord((short)Width, (short)Height);
            
            //TODO: Fix memory leak here.. 
            WriteConsoleOutput(_consoleOutBuffer, _chars, resolution, Coord.Zero, ref _writeRegion);    
        }
        
        public void Clear()
        {
            for (var i = 0; i < _chars.Length; i++)
            {
                _chars[i].UnicodeChar = ' ';
                _chars[i].Attributes = 0;
            }
        }

        public void SetTitle(string title) => SetConsoleTitle(title);
        
        public void SetCursorVisible(bool show)
        {
            GetConsoleCursorInfo(_consoleOutBuffer, out var cursorInfo);
            cursorInfo.Visible = show;
            SetConsoleCursorInfo(_consoleOutBuffer, ref cursorInfo);
            _cursorVisible = show;
        }
        
        //**********************************************************
        //** private methods:
        //**********************************************************

        private void InitBuffer(IntPtr handle)
        {
            var initSize = new SmallRect(0, 0, 1, 1);
            SetConsoleWindowInfo(handle, true, ref initSize);
            
            var size = new Coord((short)Width, (short)Height);
            
            SetConsoleScreenBufferSize(handle, size);
            
            SetConsoleActiveScreenBuffer(handle);
          
            SetFont(handle);
            
            var windowRect = new SmallRect(0, 0, (short)(Width-1), (short)(Height-1));
            SetConsoleWindowInfo(handle, true, ref windowRect);
        }
        
        private void DisableResize()
        {
            var handle = GetConsoleWindow();

            if (handle == IntPtr.Zero) 
                return;
            
            var sysMenu = GetSystemMenu(handle, false);
            DeleteMenu(sysMenu, SC_MAXIMIZE, MF_BYCOMMAND);
            DeleteMenu(sysMenu, SC_SIZE, MF_BYCOMMAND);
        }

        private void SetFont(IntPtr handle)
        {
            var font = new ConsoleFontInfoEx();
            font.cbSize = (uint)Marshal.SizeOf(font);
            font.FaceName = FontFace;
            font.dwFontSize.X = (short)FontWidth;
            font.dwFontSize.Y = (short)FontHeight;
            
            SetCurrentConsoleFontEx(handle, false, ref font);
        }

        public void Close()
        {
            Environment.Exit(0);
        }
        
        private static ushort GetColorValue(ConsoleColor fg, ConsoleColor bg) => (ushort)((int)fg | ((int)bg<<4));
    }
}