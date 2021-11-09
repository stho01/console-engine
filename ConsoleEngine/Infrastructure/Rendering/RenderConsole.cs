using System;
using Microsoft.Toolkit.HighPerformance;
using ConsoleEngine.Abstractions.Rendering;
using Microsoft.Xna.Framework;

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

        public void DrawLine(int x1, int y1, int x2, int y2, char character, ConsoleColor foregroundColor = ConsoleColor.White, ConsoleColor backgroundColor = ConsoleColor.Black)
            => DrawLine(new Point(x1, y1), new Point(x2, y2), character, foregroundColor, backgroundColor);
        public void DrawLine(Point p1, Point p2, char character, ConsoleColor foregroundColor = ConsoleColor.White, ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            // Creds goes to olc. https://github.com/OneLoneCoder
            int x, y, dx1, dy1, px, py, xe, ye, i;
            var d = p2 - p1;

            dx1 = Math.Abs(d.X); 
            dy1 = Math.Abs(d.Y);

            px = 2 * dy1 - dx1;	
            py = 2 * dx1 - dy1;

            if (dy1 <= dx1)
            {
                if (d.X >= 0) { x = p1.X; y = p1.Y; xe = p2.X; }
                else { x = p2.X; y = p2.Y; xe = p1.X;}
                
                Draw(x, y, character, foregroundColor, backgroundColor);

                for (i = 0; x < xe; i++)
                {
                    x = x + 1;
                    if (px < 0) 
                    {
                        px = px + 2 * dy1;
                    } 
                    else
                    {
                        if ((d.X < 0 && d.Y < 0) || (d.X > 0 && d.Y > 0)) 
                            y = y + 1; 
                        else 
                            y = y - 1;
                        
                        px = px + 2 * (dy1 - dx1);
                    }
                    
                    Draw(x, y, character, foregroundColor, backgroundColor);
                }
            }
            else
            {
                if (d.Y >= 0) { x = p1.X; y = p1.Y; ye = p2.Y; }
                else { x = p2.X; y = p2.Y; ye = p1.Y; }
                
                Draw(x, y, character, foregroundColor, backgroundColor);
                
                for (i = 0; y < ye; i++)
                {
                    y = y + 1;
                    if (py <= 0)
                        py = py + 2 * dx1;
                    else
                    {
                        if ((d.X < 0 && d.Y < 0) || (d.X > 0 && d.Y > 0)) 
                            x = x + 1; 
                        else 
                            x = x - 1;
                        
                        py = py + 2 * (dx1 - dy1);
                    }
                    
                    Draw(x, y, character, foregroundColor, backgroundColor);
                }
            }
        }

        public char? GetCharAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return null;
            
            return _pixels[y * Width + x].Char;
        }


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
        
        //**********************************************************
        //** internal methods:
        //**********************************************************
       
        internal void Initialize()
        {
            _consoleHandler.InitializeConsole();
            if (!Resizeable)
                _consoleHandler.Resizable(false);
            
            SetCursorVisible(!HideCursor);
        }
        
        internal void Display() => _consoleHandler.Render(_pixels);
    }
}