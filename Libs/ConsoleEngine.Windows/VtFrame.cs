using System;
using ConsoleEngine.Abstractions.Rendering;

namespace ConsoleEngine.Native;

/// <summary>
/// Builds the VT text (cursor moves, 24-bit SGR colours, characters) that turns the shown frame into the next one:
/// only cells that changed are written, a cursor move starts each run of changed cells, and a colour is sent only when
/// it differs from the one before. Pure: no console calls, no allocations after construction.
/// </summary>
internal sealed class VtFrame
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const char Esc = '\u001b';
    private const uint Unknown = 0xFFFFFFFF; // no colour sent yet in this frame
    // Worst case per cell: cursor move "ESC[rrrrr;cccccH" (14), "ESC[38;2;r;g;b;48;2;r;g;bm" (36), the character.
    private const int MaxCellLength = 51;

    private readonly int _width;
    private readonly char[] _shownChars;
    private readonly uint[] _shownForeground;
    private readonly uint[] _shownBackground;
    private readonly char[] _text;
    private bool _shownValid;
    private int _length;

    //**********************************************************
    //** ctor
    //**********************************************************

    public VtFrame(int width, int height)
    {
        _width = width;
        _shownChars = new char[width * height];
        _shownForeground = new uint[width * height];
        _shownBackground = new uint[width * height];
        _text = new char[width * height * MaxCellLength];
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>
    /// <c>0xRRGGBB</c> per <see cref="ConsoleColor"/>, for cells whose colour is <see cref="IConsoleHandler.PaletteColor"/>.
    /// </summary>
    public uint[] Palette { get; } = new uint[16];

    /// <summary>The text of the last <see cref="Build"/>.</summary>
    public ReadOnlySpan<char> Text => _text.AsSpan(0, _length);

    /// <summary><see cref="Text"/>'s array, from index 0, for the Win32 write.</summary>
    public char[] TextBuffer => _text;

    public int Length => _length;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// The screen's content is unknown (first frame, failed write, other output): the next frame writes every cell.
    /// </summary>
    public void Invalidate() => _shownValid = false;

    /// <summary>
    /// Builds <see cref="Text"/> for the cells that differ from the shown frame and records them as shown. False, with
    /// an empty <see cref="Text"/>, when nothing changed.
    /// </summary>
    public bool Build(ReadOnlySpan<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb)
    {
        _length = 0;
        var foreground = Unknown;
        var background = Unknown;
        var shownValid = _shownValid;
        var height = _shownChars.Length / _width;

        for (var y = 0; y < height; y++)
        {
            var cursorX = -1;
            var rowStart = y * _width;
            for (var x = 0; x < _width; x++)
            {
                var i = rowStart + x;
                var pixel = pixels[i];
                var fg = foregroundRgb[i] > 0xFFFFFF ? Palette[(int)pixel.ForegroundColor & 0xF] : foregroundRgb[i];
                var bg = backgroundRgb[i] > 0xFFFFFF ? Palette[(int)pixel.BackgroundColor & 0xF] : backgroundRgb[i];
                var c = Printable(pixel.Char);

                if (shownValid && _shownChars[i] == c && _shownForeground[i] == fg && _shownBackground[i] == bg)
                    continue;

                _shownChars[i] = c;
                _shownForeground[i] = fg;
                _shownBackground[i] = bg;

                if (cursorX != x)
                    AppendCursorMove(y, x);
                if (fg != foreground || bg != background)
                {
                    AppendColors(fg != foreground ? fg : Unknown, bg != background ? bg : Unknown);
                    foreground = fg;
                    background = bg;
                }

                _text[_length++] = c;
                cursorX = x + 1;
            }
        }

        _shownValid = true;
        return _length > 0;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>VT moves the cursor on control characters; WriteConsoleOutput shows them (and NUL) as blanks.</summary>
    private static char Printable(char c) => c < ' ' || c == '\u007f' || char.IsSurrogate(c) ? ' ' : c;

    private void AppendCursorMove(int y, int x)
    {
        _text[_length++] = Esc;
        _text[_length++] = '[';
        AppendNumber(y + 1);
        _text[_length++] = ';';
        AppendNumber(x + 1);
        _text[_length++] = 'H';
    }

    /// <summary>SGR for the colours that changed; <see cref="Unknown"/> leaves that one out.</summary>
    private void AppendColors(uint fg, uint bg)
    {
        _text[_length++] = Esc;
        _text[_length++] = '[';
        if (fg != Unknown)
            AppendColor('3', fg);
        if (fg != Unknown && bg != Unknown)
            _text[_length++] = ';';
        if (bg != Unknown)
            AppendColor('4', bg);
        _text[_length++] = 'm';
    }

    /// <summary><c>38;2;r;g;b</c> (foreground) or <c>48;2;r;g;b</c> (background).</summary>
    private void AppendColor(char layer, uint rgb)
    {
        _text[_length++] = layer;
        _text[_length++] = '8';
        _text[_length++] = ';';
        _text[_length++] = '2';
        _text[_length++] = ';';
        AppendNumber((int)(rgb >> 16) & 0xFF);
        _text[_length++] = ';';
        AppendNumber((int)(rgb >> 8) & 0xFF);
        _text[_length++] = ';';
        AppendNumber((int)rgb & 0xFF);
    }

    private void AppendNumber(int value)
    {
        if (value >= 10000) _text[_length++] = (char)('0' + value / 10000);
        if (value >= 1000) _text[_length++] = (char)('0' + value / 1000 % 10);
        if (value >= 100) _text[_length++] = (char)('0' + value / 100 % 10);
        if (value >= 10) _text[_length++] = (char)('0' + value / 10 % 10);
        _text[_length++] = (char)('0' + value % 10);
    }
}
