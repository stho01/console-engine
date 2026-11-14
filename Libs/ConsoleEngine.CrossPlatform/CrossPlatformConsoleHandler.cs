using System;
using System.IO;
using System.Text;
using ConsoleEngine.Abstractions.Rendering;

namespace ConsoleEngine.CrossPlatform;

/// <summary>
/// Renders to any ANSI/VT capable terminal (macOS Terminal, iTerm2, kitty, WezTerm, Ghostty, Linux terminals ...).
/// Each frame is diffed against the previous one and flushed to stdout in a single write.
///
/// Terminal cells are about twice as tall as they are wide, so in pixel mode the frame is drawn with half blocks:
/// every cell shows two pixels stacked on top of each other, as colors. If the terminal is smaller than the game
/// the middle of the frame is shown.
///
/// On a true-colour terminal (<c>COLORTERM</c> or <c>CONSOLE_ENGINE_COLORS</c>) <see cref="SetTrueColor"/> turns on
/// 24-bit frames: <see cref="Render(Span{Pixel}, ReadOnlySpan{uint}, ReadOnlySpan{uint})"/> writes each cell's RGB as
/// <c>38;2</c>/<c>48;2</c>, and <see cref="IConsoleHandler.PaletteColor"/> shows the pixel's colour from the palette.
/// </summary>
public sealed class CrossPlatformConsoleHandler : IConsoleHandler
{
    private const string Esc = "\u001b";
    private const char UpperHalfBlock = '\u2580';

    /// <summary>What each terminal cell shows now, colours as palette indices or, in a 24-bit frame, RGB.</summary>
    private Cell[] _shown;

    private readonly StringBuilder _frame = new();
    private readonly bool _trueColor;
    private bool _rgb;
    private bool _shownRgb;
    private readonly (byte R, byte G, byte B)[] _palette = [.. CampbellPalette];
    private bool _halfBlocks;
    private int _columns;
    private int _rows;
    private readonly object _writeLock = new();
    private Stream _stdout;
    private byte[] _bytes = Array.Empty<byte>();
    private bool _fullRedraw = true;
    private bool _initialized;
    private bool _restored;
    private int _visibleWidth;
    private int _visibleHeight;
    private int _originalWidth;
    private int _originalHeight;

    public CrossPlatformConsoleHandler(int width, int height) : this(width, height, DetectTrueColor())
    {
    }

    /// <param name="trueColorTerminal">Whether the terminal shows 24-bit colour (tests: no environment needed).</param>
    internal CrossPlatformConsoleHandler(int width, int height, bool trueColorTerminal)
    {
        Width = width;
        Height = height;
        _trueColor = trueColorTerminal;
        SetPixelMode(false);
    }

    public int Width { get; }
    public int Height { get; }

    public void SetPixelMode(bool pixelMode)
    {
        _halfBlocks = DetectHalfBlocks(pixelMode);
        _columns = Width;
        _rows = _halfBlocks ? (Height + 1) / 2 : Height;
        _shown = new Cell[_columns * _rows];
        _fullRedraw = true;
    }

    /// <summary>On only on a true-colour terminal; returns whether it is on.</summary>
    public bool SetTrueColor(bool trueColor) => _rgb = trueColor && _trueColor;

    /// <summary>
    /// Used by true-colour terminals only (also for <see cref="IConsoleHandler.PaletteColor"/> cells); the 16 ANSI
    /// colours are the terminal's own.
    /// </summary>
    public void SetPalette(ReadOnlySpan<uint> colors)
    {
        for (var i = 0; i < _palette.Length && i < colors.Length; i++)
            _palette[i] = ((byte)(colors[i] >> 16), (byte)(colors[i] >> 8), (byte)colors[i]);
        _fullRedraw = true;
    }

    public void InitializeConsole()
    {
        Console.OutputEncoding = Encoding.UTF8;
        _stdout = Console.OpenStandardOutput();
        _originalWidth = SafeWindowSize(() => Console.WindowWidth, 0);
        _originalHeight = SafeWindowSize(() => Console.WindowHeight, 0);
        _initialized = true;

        // ProcessExit isn't raised when the process dies from an unhandled exception.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreTerminal();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreTerminal();

        Write(
            $"{Esc}[?1049h" + // alternate screen buffer, keeps the user's scrollback intact
            $"{Esc}[?7l" +    // disable auto wrap so drawing the bottom right cell never scrolls
            $"{Esc}[8;{_rows};{_columns}t" + // ask the terminal to resize itself (ignored by terminals that don't support it)
            $"{Esc}[0m{Esc}[2J");
    }

    public void SetTitle(string title)
    {
        try { Console.Title = title; } catch { /* not supported by every terminal */ }
    }

    public void SetCursorVisible(bool visible)
    {
        if (_initialized)
            Write(visible ? $"{Esc}[?25h" : $"{Esc}[?25l");
        else
            try { Console.CursorVisible = visible; } catch { /* ignore */ }
    }

    public void Resizable(bool resizable)
    {
        // Terminal windows are owned by the terminal emulator, can't be locked from here.
    }

    public void SetFont(FontInfo fontInfo)
    {
        // Font face and size are owned by the terminal emulator, can't be changed from here.
    }

    public void Render(Span<Pixel> pixels) => Render(pixels, default, default, false);

    /// <summary>A 24-bit frame while <see cref="SetTrueColor"/> is on, else <see cref="Render(Span{Pixel})"/>.</summary>
    public void Render(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb) =>
        Render(pixels, foregroundRgb, backgroundRgb, _rgb);

    public void Close() => RestoreTerminal();

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The text of the last frame <see cref="BuildFrame"/> built.</summary>
    internal StringBuilder Frame => _frame;

    /// <summary>
    /// Writes the cells that changed since the last frame into <see cref="Frame"/>, for a terminal showing
    /// <paramref name="visibleWidth"/> × <paramref name="visibleHeight"/> cells. With <paramref name="rgb"/> the colours
    /// come from <paramref name="foregroundRgb"/>/<paramref name="backgroundRgb"/> (<c>0xRRGGBB</c>, or
    /// <see cref="IConsoleHandler.PaletteColor"/> for the pixel's own). False when nothing changed.
    /// </summary>
    internal bool BuildFrame(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb,
        bool rgb, int visibleWidth, int visibleHeight)
    {
        if (visibleWidth != _visibleWidth || visibleHeight != _visibleHeight || rgb != _shownRgb)
        {
            _visibleWidth = visibleWidth;
            _visibleHeight = visibleHeight;
            _shownRgb = rgb;
            _fullRedraw = true;
        }

        _frame.Clear();
        _frame.Append(Esc).Append("[?2026h"); // begin synchronized update (prevents tearing where supported)

        if (_fullRedraw)
            _frame.Append(Esc).Append("[0m").Append(Esc).Append("[2J");

        var currentFg = -1L;
        var currentBg = -1L;
        var changed = _fullRedraw;

        // Show the middle of the frame when the terminal is too small for all of it.
        var offsetX = (_columns - visibleWidth) / 2;
        var offsetY = (_rows - visibleHeight) / 2;

        for (var y = 0; y < visibleHeight; y++)
        {
            var cursorX = -1;
            for (var x = 0; x < visibleWidth; x++)
            {
                var cell = GetCell(pixels, foregroundRgb, backgroundRgb, rgb, x + offsetX, y + offsetY);
                ref var shown = ref _shown[(y + offsetY) * _columns + x + offsetX];

                if (!_fullRedraw && shown.Equals(cell))
                    continue;

                changed = true;
                shown = cell;

                if (cursorX != x)
                    _frame.Append(Esc).Append('[').Append(y + 1).Append(';').Append(x + 1).Append('H');

                if (cell.Foreground != currentFg || cell.Background != currentBg)
                {
                    AppendColors(cell.Foreground, cell.Background, rgb);
                    currentFg = cell.Foreground;
                    currentBg = cell.Background;
                }

                _frame.Append(cell.Char);
                cursorX = x + 1;
            }
        }

        if (!changed)
            return false;

        _frame.Append(Esc).Append("[0m");
        _frame.Append(Esc).Append("[?2026l"); // end synchronized update
        _fullRedraw = false;
        return true;
    }

    //**********************************************************
    //** private:
    //**********************************************************

    private void Render(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb,
        bool rgb)
    {
        if (!_initialized)
            return;

        var visibleWidth = Math.Min(_columns, SafeWindowSize(() => Console.WindowWidth, _columns));
        var visibleHeight = Math.Min(_rows, SafeWindowSize(() => Console.WindowHeight, _rows));
        if (BuildFrame(pixels, foregroundRgb, backgroundRgb, rgb, visibleWidth, visibleHeight))
            Write(_frame);
    }


    private void RestoreTerminal()
    {
        if (!_initialized)
            return;

        lock (_writeLock)
        {
            if (_restored)
                return;

            try
            {
                var restoreSize = _originalWidth > 0 && _originalHeight > 0 ? $"{Esc}[8;{_originalHeight};{_originalWidth}t" : "";
                _stdout.Write(Encoding.UTF8.GetBytes($"{Esc}[?2026l{Esc}[0m{Esc}[?7h{Esc}[?25h{Esc}[?1049l{restoreSize}"));
                _stdout.Flush();
            }
            catch
            {
                // stdout might already be gone during shutdown
            }

            _restored = true;
        }
    }

    private void Write(string value)
    {
        lock (_writeLock)
        {
            if (_restored)
                return;

            _stdout.Write(Encoding.UTF8.GetBytes(value));
            _stdout.Flush();
        }
    }

    private void Write(StringBuilder value)
    {
        lock (_writeLock)
        {
            if (_restored)
                return;

            var maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
            if (_bytes.Length < maxBytes)
                _bytes = new byte[maxBytes * 2];

            var count = 0;
            foreach (var chunk in value.GetChunks())
                count += Encoding.UTF8.GetBytes(chunk.Span, _bytes.AsSpan(count));

            _stdout.Write(_bytes, 0, count);
            _stdout.Flush();
        }
    }

    /// <summary>The SGR for a cell's colours: palette indices, or with <paramref name="rgb"/> <c>0xRRGGBB</c>.</summary>
    private void AppendColors(uint fg, uint bg, bool rgb)
    {
        if (rgb)
        {
            _frame.Append(Esc).Append("[38;2;").Append((byte)(fg >> 16)).Append(';').Append((byte)(fg >> 8))
                .Append(';').Append((byte)fg).Append(";48;2;").Append((byte)(bg >> 16)).Append(';')
                .Append((byte)(bg >> 8)).Append(';').Append((byte)bg).Append('m');
        }
        else if (_trueColor)
        {
            var (fr, fgG, fb) = _palette[fg];
            var (br, bgG, bb) = _palette[bg];
            _frame.Append(Esc).Append("[38;2;").Append(fr).Append(';').Append(fgG).Append(';').Append(fb)
                .Append(";48;2;").Append(br).Append(';').Append(bgG).Append(';').Append(bb).Append('m');
        }
        else
        {
            _frame.Append(Esc).Append('[').Append(AnsiColor[fg]).Append(';').Append(AnsiColor[bg] + 10).Append('m');
        }
    }

    /// <summary>The character and colours to show in a terminal cell.</summary>
    private Cell GetCell(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb,
        bool rgb, int column, int row)
    {
        if (!_halfBlocks)
        {
            var i = row * Width + column;
            var pixel = i < pixels.Length ? pixels[i] : default;
            return new Cell(Sanitize(pixel.Char), Color(pixel.ForegroundColor, foregroundRgb, i, rgb),
                Color(pixel.BackgroundColor, backgroundRgb, i, rgb));
        }

        var top = PixelColor(pixels, foregroundRgb, backgroundRgb, rgb, column, row * 2);
        var bottom = PixelColor(pixels, foregroundRgb, backgroundRgb, rgb, column, row * 2 + 1);
        return new Cell(top == bottom ? ' ' : UpperHalfBlock, top, bottom);
    }

    /// <summary>A pixel's color in half block mode: its foreground if anything is drawn there, its background otherwise.</summary>
    private uint PixelColor(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb,
        bool rgb, int x, int y)
    {
        var i = y * Width + x;
        if (y >= Height || i >= pixels.Length)
            return Color(ConsoleColor.Black, default, i, rgb);

        var pixel = pixels[i];
        return Sanitize(pixel.Char) == ' '
            ? Color(pixel.BackgroundColor, backgroundRgb, i, rgb)
            : Color(pixel.ForegroundColor, foregroundRgb, i, rgb);
    }

    /// <summary>
    /// A cell's colour as <see cref="AppendColors"/> takes it: the palette index, or with <paramref name="rgb"/> the
    /// cell's <c>0xRRGGBB</c>, the palette's RGB of <paramref name="color"/> for <see cref="IConsoleHandler.PaletteColor"/>.
    /// </summary>
    private uint Color(ConsoleColor color, ReadOnlySpan<uint> cellRgb, int i, bool rgb)
    {
        var index = (int)color & 0xF;
        if (!rgb)
            return (uint)index;

        if ((uint)i < (uint)cellRgb.Length && cellRgb[i] != IConsoleHandler.PaletteColor)
            return cellRgb[i] & 0xFFFFFF;

        var (r, g, b) = _palette[index];
        return (uint)(r << 16 | g << 8 | b);
    }

    /// <summary>
    /// Windows renders NUL and control characters as blanks, terminals don't (they'd shift the rest of the line).
    /// </summary>
    private static char Sanitize(char c) => c < ' ' || c == '\u007f' || char.IsSurrogate(c) ? ' ' : c;

    private static int SafeWindowSize(Func<int> getter, int fallback)
    {
        try
        {
            var size = getter();
            return size > 0 ? size : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static bool DetectHalfBlocks(bool pixelMode)
    {
        var setting = Environment.GetEnvironmentVariable("CONSOLE_ENGINE_RENDER");
        if (!string.IsNullOrEmpty(setting))
            return setting.Equals("halfblock", StringComparison.OrdinalIgnoreCase);

        return pixelMode;
    }

    private static bool DetectTrueColor()
    {
        var setting = Environment.GetEnvironmentVariable("CONSOLE_ENGINE_COLORS");
        if (!string.IsNullOrEmpty(setting))
            return setting.Equals("truecolor", StringComparison.OrdinalIgnoreCase);

        var colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
        return colorTerm is "truecolor" or "24bit";
    }

    /// <summary>ConsoleColor (index) to ANSI SGR foreground color.</summary>
    private static readonly int[] AnsiColor =
    [
        30, // Black
        34, // DarkBlue
        32, // DarkGreen
        36, // DarkCyan
        31, // DarkRed
        35, // DarkMagenta
        33, // DarkYellow
        37, // Gray
        90, // DarkGray
        94, // Blue
        92, // Green
        96, // Cyan
        91, // Red
        95, // Magenta
        93, // Yellow
        97, // White
    ];

    /// <summary>ConsoleColor (index) to RGB, the Windows console (Campbell) palette so games look the same as on Windows.</summary>
    private static readonly (byte R, byte G, byte B)[] CampbellPalette =
    [
        (0, 0, 0),       // Black
        (0, 55, 218),    // DarkBlue
        (19, 161, 14),   // DarkGreen
        (58, 150, 221),  // DarkCyan
        (197, 15, 31),   // DarkRed
        (136, 23, 152),  // DarkMagenta
        (193, 156, 0),   // DarkYellow
        (204, 204, 204), // Gray
        (118, 118, 118), // DarkGray
        (59, 120, 255),  // Blue
        (22, 198, 12),   // Green
        (97, 214, 214),  // Cyan
        (231, 72, 86),   // Red
        (180, 0, 158),   // Magenta
        (249, 241, 165), // Yellow
        (242, 242, 242), // White
    ];

    /// <summary>A terminal cell: its character and colours (palette indices, or RGB in a 24-bit frame).</summary>
    private readonly record struct Cell(char Char, uint Foreground, uint Background);
}
