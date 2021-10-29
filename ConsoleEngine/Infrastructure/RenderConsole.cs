using System;
using System.Runtime.InteropServices;
using ConsoleEngine.Extensions;
using ConsoleEngine.LowLevel;

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

        public void Draw(int x, int y, Sprite sprite) => Draw(x, y, sprite.Data);
        
        public void Draw(int x, int y, char[,] data)
        {
            var d = data.Transpose();
            for (var dataX = 0; dataX < d.GetLength(0); dataX++)
            for (var dataY = 0; dataY < d.GetLength(1); dataY++) {
                Draw(dataX + x, dataY + y, d[dataX, dataY]);
            }
        }
        
        public void Draw(int x, int y, short charCode) => Draw(x, y, (char)charCode);
        
        public void Draw(int x, int y, char character)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                var index = y * Width + x;
                _chars[index].UnicodeChar = character;
                _chars[index].Attributes = (ushort)(CharAttributes.FOREGROUND_GREEN | CharAttributes.FOREGROUND_INTENSITY); 
            }
        }

        public void Draw(int x, int y, string[] mapTiles)
        {
            for (var dataY = 0; dataY < mapTiles.Length; dataY++)
            for (var dataX = 0; dataX < mapTiles[dataY].Length; dataX++)
            {
                var index = (dataY + y) * Width + (dataX + x);
                _chars[index].UnicodeChar = mapTiles[dataY][dataX];
                _chars[index].Attributes = (ushort)(CharAttributes.FOREGROUND_GREEN | CharAttributes.FOREGROUND_INTENSITY); 
            }
        }
        
        
        public char GetCharAt(int x, int y) => _chars[y * Width + x].UnicodeChar;
        
        internal void Display()
        {
            var resolution = new Coord((short)Width, (short)Height);
            WriteConsoleOutput(_consoleOutBuffer, _chars, resolution, Coord.Zero, ref _writeRegion);
        }
        
        public void Clear()
        {
            for (var i = 0; i < _chars.Length; i++)
            {
                _chars[i].UnicodeChar = ' ';
                _chars[i].Attributes = (ushort)CharAttributes.None;
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

    }
}