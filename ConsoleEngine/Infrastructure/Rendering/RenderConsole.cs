using System;
using System.Numerics;
using Microsoft.Toolkit.HighPerformance;
using ConsoleEngine.Abstractions;
using ConsoleEngine.Abstractions.Rendering;

namespace ConsoleEngine.Infrastructure.Rendering
{
    public class RenderConsole
    {
        //**********************************************************
        //** fields:
        //**********************************************************
        
        private readonly IConsoleHandler _consoleHandler;
        private readonly Pixel[] _pixels;
        private FontInfo _fontInfo;
        
        //**********************************************************
        //** ctor:
        //**********************************************************

        public RenderConsole(IConsoleHandler consoleHandler)
        {
            _consoleHandler = consoleHandler;
            _pixels = new Pixel[consoleHandler.Width * consoleHandler.Height];
            Area = _consoleHandler.Width * _consoleHandler.Height;
            FontInfo = new FontInfo
            {
                FontFace = "Consolas",
                FontWidth = 14,
                FontHeight = 14
            };
            HideCursor = true;
        }
          
        //**********************************************************
        //** props:
        //**********************************************************

        public int Width => _consoleHandler.Width;
        public int Height => _consoleHandler.Height;
        public Vector2 ScreenCenter => new Vector2(Width / 2, Height / 2);
        
        /// <summary> The total Screen area. W * H </summary>
        public int Area { get; }

        public FontInfo FontInfo
        {
            get => _fontInfo;
            set
            {
                _consoleHandler.SetFont(value);
                _fontInfo = value;
            }
        }

        public bool Resizeable { get; init; }
        public bool HideCursor { get; init; }
        
        //**********************************************************
        //** public methods:
        //**********************************************************

        internal RenderConsole Initialize()
        {
            _consoleHandler.InitializeConsole();
            if (!Resizeable)
                _consoleHandler.Resizable(false);
            
            SetCursorVisible(!HideCursor);
            
            return this;
        }

        public void Draw(int x, int y, Sprite sprite)
        {
            Draw(x, y, sprite.DataSpan);
        }
        
        public void Draw(int x, int y, Span2D<Pixel> pixels)
        {
            for (var dataX = 0; dataX < pixels.Height; dataX++)
            for (var dataY = 0; dataY < pixels.Width; dataY++) {
                var pixel = pixels[dataX, dataY];
                Draw(dataX + x, dataY + y, pixel.Char, pixel.ForegroundColor, pixel.BackgroundColor);
            }  
        }

        public void Draw(int x, int y, Span2D<char> data, ConsoleColor foregroundColor = ConsoleColor.White, ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            for (var dataX = 0; dataX < data.Height; dataX++)
            for (var dataY = 0; dataY < data.Width; dataY++) {
                Draw(dataX + x, dataY + y, data[dataX, dataY], foregroundColor, backgroundColor);
            }
        }

        public void Draw(int x, int y, string value, ConsoleColor foregroundColor = ConsoleColor.White, ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            for (var i = 0; i < value.Length; i++)
                Draw(x+i, y, value[i], foregroundColor, backgroundColor);
        }
        
        public void Draw(int x, int y, short charCode) => Draw(x, y, (char)charCode);

        public void Draw(int x, int y, char character, ConsoleColor foregroundColor = ConsoleColor.White, ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                var index = y * Width + x;
                _pixels[index].Char = character;
                _pixels[index].ForegroundColor = foregroundColor;
                _pixels[index].BackgroundColor = backgroundColor;
            }
        }

        public void Draw(int x, int y, Span<string> rows, ConsoleColor foregroundColor = ConsoleColor.White, ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            for (var dataY = 0; dataY < rows.Length; dataY++)
            for (var dataX = 0; dataX < rows[dataY].Length; dataX++)
            {
                var index = (dataY + y) * Width + (dataX + x);
                _pixels[index].Char = rows[dataY][dataX]; // < rows & cols are flipped 
                _pixels[index].ForegroundColor = foregroundColor;
                _pixels[index].BackgroundColor = backgroundColor;
            }
        }
   
        public char? GetCharAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return null;
            
            return _pixels[y * Width + x].Char;
        }

        internal void Display() => _consoleHandler.Render(_pixels);

        public void Clear()
        {
            for (var i = 0; i < _pixels.Length; i++) {
                _pixels[i] = Pixel.Blank;
            }
        }

        public void SetTitle(string title) => _consoleHandler.SetTitle(title);
        public void SetCursorVisible(bool show) => _consoleHandler.SetCursorVisible(show);

        public void Close() {
            Environment.Exit(0);
        }
    }
}