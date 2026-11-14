using System;
using CommunityToolkit.HighPerformance;
using ConsoleEngine.Abstractions.Rendering;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Infrastructure.Rendering;

public class RenderConsole
{
    //**********************************************************
    //** fields:
    //**********************************************************
        
    private readonly IConsoleHandler _consoleHandler;
    private readonly Pixel[] _pixels;
    private FontInfo _fontInfo;
    private Pixel _clearColor = Pixel.Blank;
    private uint[] _palette;
    private bool _trueColor;
    private bool _initialized;
    private uint[] _foregroundRgb = [];   // allocated the first time true colour is on
    private uint[] _backgroundRgb = [];

    /// <summary>The Windows console's default colours, for <see cref="DrawRgb"/> without a <see cref="Palette"/>.</summary>
    private static readonly uint[] CampbellPalette =
    [
        0x000000, 0x0037DA, 0x13A10E, 0x3A96DD, 0xC50F1F, 0x881798, 0xC19C00, 0xCCCCCC,
        0x767676, 0x3B78FF, 0x16C60C, 0x61D6D6, 0xE74856, 0xB4009E, 0xF9F1A5, 0xF2F2F2
    ];
        
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
    public Vector2 ScreenCenter => new((Width / 2f), (Height / 2f));

    public ConsoleColor ClearColor
    {
        get => _clearColor.BackgroundColor;
        set => _clearColor = new Pixel { BackgroundColor = value, Char = ' ' };
    } 
        
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

    /// <summary>
    /// The game draws pixels rather than text. Terminals (macOS/Linux) then show two square pixels per character
    /// cell, as colors, instead of the characters. Set it before the game is initialized. No effect on Windows,
    /// where the font itself is square.
    /// </summary>
    public bool PixelMode { get; set; }

    /// <summary>
    /// The RGB colour each <see cref="ConsoleColor"/> shows as: 16 entries, <c>0xRRGGBB</c>, in <see cref="ConsoleColor"/>
    /// order. Null (the default) keeps the console's own colours. Set it before the game is initialized. Windows and
    /// true-colour terminals apply it, others ignore it.
    /// </summary>
    public uint[] Palette
    {
        get => _palette;
        set
        {
            if (value != null && value.Length != 16)
                throw new ArgumentException("A palette needs exactly 16 colours.", nameof(value));
            _palette = value;
        }
    }

    /// <summary>
    /// Ask for 24-bit colour: <see cref="DrawRgb"/> then shows its exact colours. Set it before the game is initialized
    /// or while it runs. Off (the default) is the 16-colour console. Whether the console could do it is
    /// <see cref="TrueColorActive"/>.
    /// </summary>
    public bool TrueColor
    {
        get => _trueColor;
        set
        {
            _trueColor = value;
            if (_initialized)
                ApplyTrueColor();
        }
    }

    /// <summary>
    /// True when the console shows 24-bit colour (after initialization, with <see cref="TrueColor"/> on). False when it
    /// can't (an old Windows console, a terminal without it): <see cref="DrawRgb"/> then draws the nearest of the 16
    /// colours.
    /// </summary>
    public bool TrueColorActive { get; private set; }
        
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

    public void Draw(int x, int y, Pixel pixel)
    {
        Draw(x, y, pixel.Char, pixel.ForegroundColor, pixel.BackgroundColor);
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
            if (TrueColorActive)
                UsePalette(index, 1);
        }
    }

    /// <summary>
    /// Draws a character in 24-bit colour, <c>0xRRGGBB</c>. Shown exactly while <see cref="TrueColorActive"/>,
    /// otherwise as the nearest <see cref="Palette"/> colour (the console's default colours without one).
    /// </summary>
    public void DrawRgb(int x, int y, char character, uint foregroundRgb, uint backgroundRgb)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        var index = y * Width + x;
        _pixels[index].Char = character;
        if (TrueColorActive)
        {
            _foregroundRgb[index] = foregroundRgb & 0xFFFFFF;
            _backgroundRgb[index] = backgroundRgb & 0xFFFFFF;
            return;
        }

        _pixels[index].ForegroundColor = NearestColor(foregroundRgb);
        _pixels[index].BackgroundColor = NearestColor(backgroundRgb);
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
            if (TrueColorActive)
                UsePalette(index, 1);
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
                x += 1;
                
                if (px < 0) 
                {
                    px += 2 * dy1;
                } 
                else
                {
                    if ((d.X < 0 && d.Y < 0) || (d.X > 0 && d.Y > 0)) 
                        y += 1; 
                    else 
                        y -= 1;
                        
                    px += 2 * (dy1 - dx1);
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
                y += 1;
                if (py <= 0)
                    py += 2 * dx1;
                else
                {
                    if ((d.X < 0 && d.Y < 0) || (d.X > 0 && d.Y > 0)) 
                        x += 1; 
                    else 
                        x -= 1;
                        
                    py += 2 * (dx1 - dy1);
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
            _pixels[i] = _clearColor;
        }
        if (TrueColorActive)
            UsePalette(0, _pixels.Length);
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
        _consoleHandler.SetPixelMode(PixelMode);
        _consoleHandler.InitializeConsole();
        if (_palette != null)
            _consoleHandler.SetPalette(_palette);
        if (!Resizeable)
            _consoleHandler.Resizable(false);
            
        SetCursorVisible(!HideCursor);
        _initialized = true;
        if (_trueColor)
            ApplyTrueColor();
    }

    internal void Display()
    {
        if (TrueColorActive)
            _consoleHandler.Render(_pixels, _foregroundRgb, _backgroundRgb);
        else
            _consoleHandler.Render(_pixels);
    }

    /// <summary>
    /// Cells from <paramref name="start"/> show their <see cref="Pixel"/>'s colours again, not the last
    /// <see cref="DrawRgb"/> ones. For code that writes <see cref="Pixels"/> directly. No-op without true colour.
    /// </summary>
    internal void UsePalette(int start, int length)
    {
        if (!TrueColorActive)
            return;
        _foregroundRgb.AsSpan(start, length).Fill(IConsoleHandler.PaletteColor);
        _backgroundRgb.AsSpan(start, length).Fill(IConsoleHandler.PaletteColor);
    }

    /// <summary>
    /// The frame buffer, index <c>y * Width + x</c>. For the engine's ECS render systems, which clip once per entity
    /// instead of calling <see cref="Draw(int, int, char, ConsoleColor, ConsoleColor)"/> (and its bounds check) per cell.
    /// </summary>
    internal Span<Pixel> Pixels => _pixels;

    internal void Restore() => _consoleHandler.Close();

    private void ApplyTrueColor()
    {
        TrueColorActive = _consoleHandler.SetTrueColor(_trueColor);
        if (!TrueColorActive)
            return;

        if (_foregroundRgb.Length == 0)
        {
            _foregroundRgb = new uint[_pixels.Length];
            _backgroundRgb = new uint[_pixels.Length];
        }
        UsePalette(0, _pixels.Length); // what is drawn so far was drawn in palette colours
    }

    /// <summary>The <see cref="ConsoleColor"/> closest to <paramref name="rgb"/> (squared RGB distance).</summary>
    private ConsoleColor NearestColor(uint rgb)
    {
        var palette = _palette ?? CampbellPalette;
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var dr = (int)((rgb >> 16) & 0xFF) - (int)((palette[i] >> 16) & 0xFF);
            var dg = (int)((rgb >> 8) & 0xFF) - (int)((palette[i] >> 8) & 0xFF);
            var db = (int)(rgb & 0xFF) - (int)(palette[i] & 0xFF);
            var distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return (ConsoleColor)best;
    }
}