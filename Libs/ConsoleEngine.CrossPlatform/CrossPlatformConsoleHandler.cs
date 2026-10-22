using System;
using System.IO;
using System.Text;
using ConsoleEngine.Abstractions.Rendering;

namespace ConsoleEngine.CrossPlatform;

/// <summary>
/// Renders to any ANSI/VT capable terminal (macOS Terminal, iTerm2, kitty, WezTerm, Ghostty, Linux terminals ...).
/// Each frame is diffed against the previous one and flushed to stdout in a single write.
/// </summary>
public sealed class CrossPlatformConsoleHandler : IConsoleHandler
{
    private const string Esc = "\u001b";

    private readonly FontInfo _fontInfo;
    private readonly Pixel[] _previous;
    private readonly StringBuilder _frame = new();
    private readonly bool _trueColor;
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

    public CrossPlatformConsoleHandler(int width, int height, FontInfo fontInfo)
    {
        _fontInfo = fontInfo;
        Width = width;
        Height = height;
        _previous = new Pixel[width * height];
        _trueColor = DetectTrueColor();
    }

    public int Width { get; }
    public int Height { get; }

    public void InitializeConsole()
    {
        Console.OutputEncoding = Encoding.UTF8;
        _stdout = Console.OpenStandardOutput();
        _originalWidth = SafeWindowSize(() => Console.WindowWidth, 0);
        _originalHeight = SafeWindowSize(() => Console.WindowHeight, 0);
        _initialized = true;

        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreTerminal();
        Console.CancelKeyPress += (_, _) => RestoreTerminal();

        Write(
            $"{Esc}[?1049h" + // alternate screen buffer, keeps the user's scrollback intact
            $"{Esc}[?7l" +    // disable auto wrap so drawing the bottom right cell never scrolls
            $"{Esc}[8;{Height};{Width}t" + // ask the terminal to resize itself (ignored by terminals that don't support it)
            $"{Esc}[0m{Esc}[2J");

        SetFont(_fontInfo);
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

    public void Render(Span<Pixel> pixels)
    {
        if (!_initialized)
            return;

        var visibleWidth = Math.Min(Width, SafeWindowSize(() => Console.WindowWidth, Width));
        var visibleHeight = Math.Min(Height, SafeWindowSize(() => Console.WindowHeight, Height));
        if (visibleWidth != _visibleWidth || visibleHeight != _visibleHeight)
        {
            _visibleWidth = visibleWidth;
            _visibleHeight = visibleHeight;
            _fullRedraw = true;
        }

        _frame.Clear();
        _frame.Append(Esc).Append("[?2026h"); // begin synchronized update (prevents tearing where supported)

        if (_fullRedraw)
            _frame.Append(Esc).Append("[0m").Append(Esc).Append("[2J");

        var currentFg = -1;
        var currentBg = -1;
        var changed = _fullRedraw;

        for (var y = 0; y < visibleHeight; y++)
        {
            var cursorX = -1;
            for (var x = 0; x < visibleWidth; x++)
            {
                var i = y * Width + x;
                if (i >= pixels.Length)
                    break;

                var pixel = pixels[i];
                var c = Sanitize(pixel.Char);
                ref var previous = ref _previous[i];

                if (!_fullRedraw
                    && previous.Char == c
                    && previous.ForegroundColor == pixel.ForegroundColor
                    && previous.BackgroundColor == pixel.BackgroundColor)
                    continue;

                changed = true;
                previous.Char = c;
                previous.ForegroundColor = pixel.ForegroundColor;
                previous.BackgroundColor = pixel.BackgroundColor;

                if (cursorX != x)
                    _frame.Append(Esc).Append('[').Append(y + 1).Append(';').Append(x + 1).Append('H');

                var fg = (int)pixel.ForegroundColor & 0xF;
                var bg = (int)pixel.BackgroundColor & 0xF;
                if (fg != currentFg || bg != currentBg)
                {
                    AppendColors(fg, bg);
                    currentFg = fg;
                    currentBg = bg;
                }

                _frame.Append(c);
                cursorX = x + 1;
            }
        }

        if (!changed)
            return;

        _frame.Append(Esc).Append("[0m");
        _frame.Append(Esc).Append("[?2026l"); // end synchronized update
        _fullRedraw = false;

        Write(_frame);
    }

    public void Close() => RestoreTerminal();

    //**********************************************************
    //** private:
    //**********************************************************

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

    private void AppendColors(int fg, int bg)
    {
        if (_trueColor)
        {
            var (fr, fgG, fb) = Palette[fg];
            var (br, bgG, bb) = Palette[bg];
            _frame.Append(Esc).Append("[38;2;").Append(fr).Append(';').Append(fgG).Append(';').Append(fb)
                .Append(";48;2;").Append(br).Append(';').Append(bgG).Append(';').Append(bb).Append('m');
        }
        else
        {
            _frame.Append(Esc).Append('[').Append(AnsiColor[fg]).Append(';').Append(AnsiColor[bg] + 10).Append('m');
        }
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
    private static readonly (byte R, byte G, byte B)[] Palette =
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
}
