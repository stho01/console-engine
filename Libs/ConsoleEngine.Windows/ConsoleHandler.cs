using System;
using System.Runtime.InteropServices;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Native.LowLevel;

namespace ConsoleEngine.Native;

using static Kernel32;
using static User32;
    
public sealed class ConsoleHandler : IConsoleHandler
{
    private readonly FontInfo _fontInfo;
    private readonly IntPtr _consoleOutBuffer;
    private CharInfo[] _shownChars;   // what the console shows, valid while _shownCharsValid
    private CharInfo[] _nextChars;    // the frame being converted, swapped with _shownChars after the write
    private bool _shownCharsValid;
    private uint[] _originalColorTable;
    private VtFrame _vtFrame;          // made the first time 24-bit colour is turned on
    private bool _vtOn;
    private uint _originalMode;
    private bool _restoreModeOnExit;

    public ConsoleHandler(int width, int height, FontInfo fontInfo)
    {
        _fontInfo = fontInfo;
        Width = width;
        Height = height;
        _shownChars = new CharInfo[width*height];
        _nextChars = new CharInfo[width*height];
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
        _shownCharsValid = false;
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

    /// <summary>
    /// Writes only the rows from the first to the last one that changed since the last frame, in one
    /// <c>WriteConsoleOutput</c>. conhost's cost scales with the rows written, even when they are unchanged.
    /// </summary>
    public void Render(Span<Pixel> pixels)
    {
        _vtFrame?.Invalidate();
        var next = _nextChars;
        for (var i = 0; i < pixels.Length; i++)
        {
            next[i].UnicodeChar = pixels[i].Char;
            next[i].Attributes = GetColorValue(pixels[i].ForegroundColor, pixels[i].BackgroundColor);
        }

        var top = 0;
        var bottom = Height - 1;
        if (_shownCharsValid && !FindChangedRows(MemoryMarshal.Cast<CharInfo, uint>(next),
                MemoryMarshal.Cast<CharInfo, uint>(_shownChars), Width, out top, out bottom))
            return;

        var resolution = new Coord((short)Width, (short)Height);
        var region = new SmallRect(0, (short)top, (short)(Width - 1), (short)bottom);
        // A failed write leaves the screen unknown: the next frame writes all rows.
        _shownCharsValid = WriteConsoleOutput(_consoleOutBuffer, next, resolution, new Coord(0, (short)top), ref region);
        _nextChars = _shownChars;
        _shownChars = next;
    }

    /// <summary>
    /// The first and last row of <paramref name="width"/> cells where <paramref name="next"/> differs from
    /// <paramref name="shown"/>. False when they are equal.
    /// </summary>
    internal static bool FindChangedRows(ReadOnlySpan<uint> next, ReadOnlySpan<uint> shown, int width,
        out int top, out int bottom)
    {
        var equalPrefix = next.CommonPrefixLength(shown);
        if (equalPrefix == next.Length)
        {
            top = bottom = -1;
            return false;
        }

        top = equalPrefix / width;
        bottom = next.Length / width - 1;
        while (bottom > top && next.Slice(bottom * width, width).SequenceEqual(shown.Slice(bottom * width, width)))
            bottom--;
        return true;
    }

    /// <summary>
    /// Turns VT processing (<c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c>) on for 24-bit colour, or gives the console its
    /// own mode back. False when the console can't do VT (Windows before 10 1607, or output redirected).
    /// </summary>
    public bool SetTrueColor(bool trueColor)
    {
        if (!trueColor)
        {
            if (_vtOn)
            {
                RestoreMode();
                _vtOn = false;
                _shownCharsValid = false;
            }
            return false;
        }

        if (_vtOn)
            return true;
        if (!GetConsoleMode(_consoleOutBuffer, out var mode))
            return false;

        // No wrap: writing the last column then never scrolls the screen.
        var vtMode = (mode | ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING) & ~ENABLE_WRAP_AT_EOL_OUTPUT;
        if (!SetConsoleMode(_consoleOutBuffer, vtMode))
            return false;

        _originalMode = mode;
        if (!_restoreModeOnExit)
        {
            _restoreModeOnExit = true;
            // Stop() ends the process with Environment.Exit, which skips Close().
            AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreMode();
        }

        _vtFrame ??= new VtFrame(Width, Height);
        _vtFrame.Invalidate();
        ReadColorTable(_vtFrame.Palette);
        _vtOn = true;
        return true;
    }

    /// <summary>
    /// Writes the changed cells as VT text (cursor moves, <c>38;2</c>/<c>48;2</c> colours) in one <c>WriteConsole</c>.
    /// Falls back to <see cref="Render(Span{Pixel})"/> while 24-bit colour is off.
    /// </summary>
    public void Render(Span<Pixel> pixels, ReadOnlySpan<uint> foregroundRgb, ReadOnlySpan<uint> backgroundRgb)
    {
        if (!_vtOn)
        {
            Render(pixels);
            return;
        }

        _shownCharsValid = false;
        if (!_vtFrame.Build(pixels, foregroundRgb, backgroundRgb))
            return;

        // A failed or short write leaves the screen unknown: the next frame writes every cell.
        if (!WriteConsole(_consoleOutBuffer, _vtFrame.TextBuffer, (uint)_vtFrame.Length, out var written, IntPtr.Zero)
            || written != _vtFrame.Length)
            _vtFrame.Invalidate();
    }

    public void SetPalette(ReadOnlySpan<uint> colors)
    {
        if (!TryGetBufferInfo(out var info))
            return;

        if (_originalColorTable == null)
        {
            _originalColorTable = (uint[])info.ColorTable.Clone();
            // Stop() ends the process with Environment.Exit, which skips Close().
            AppDomain.CurrentDomain.ProcessExit += (_, _) => RestorePalette();
        }

        for (var i = 0; i < info.ColorTable.Length && i < colors.Length; i++)
        {
            var rgb = colors[i];
            info.ColorTable[i] = ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF); // RRGGBB -> COLORREF
        }

        SetBufferInfo(ref info);
        if (_vtOn)
        {
            ReadColorTable(_vtFrame.Palette);
            _vtFrame.Invalidate();
        }
    }

    public void Close()
    {
        RestorePalette();
        RestoreMode();
    }

    private void RestoreMode()
    {
        if (!_vtOn)
            return;
        SetConsoleMode(_consoleOutBuffer, _originalMode); // nothing left to do if it fails: the process is ending
    }

    /// <summary>The console's 16 colours as <c>0xRRGGBB</c>, for cells drawn in a <see cref="ConsoleColor"/>.</summary>
    private void ReadColorTable(uint[] rgb)
    {
        if (!TryGetBufferInfo(out var info))
            return; // keeps the previous (or black) colours: only palette-coloured cells are affected
        for (var i = 0; i < rgb.Length && i < info.ColorTable.Length; i++)
        {
            var colorRef = info.ColorTable[i];
            rgb[i] = ((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF);
        }
    }

    /// <summary>Gives the console its own colours back, if <see cref="SetPalette"/> changed them.</summary>
    private void RestorePalette()
    {
        if (_originalColorTable == null || !TryGetBufferInfo(out var info))
            return;

        info.ColorTable = _originalColorTable;
        SetBufferInfo(ref info);
    }

    private bool TryGetBufferInfo(out ConsoleScreenBufferInfoEx info)
    {
        info = new ConsoleScreenBufferInfoEx { cbSize = (uint)Marshal.SizeOf<ConsoleScreenBufferInfoEx>() };
        return GetConsoleScreenBufferInfoEx(_consoleOutBuffer, ref info);
    }

    private void SetBufferInfo(ref ConsoleScreenBufferInfoEx info)
    {
        // SetConsoleScreenBufferInfoEx shrinks the window by one cell (a long-standing conhost quirk) unless right
        // and bottom are grown by one first.
        var window = info.srWindow;
        info.srWindow = new SmallRect(window.Left, window.Top, (short)(window.Right + 1), (short)(window.Bottom + 1));
        SetConsoleScreenBufferInfoEx(_consoleOutBuffer, ref info);
    }

    private static ushort GetColorValue(ConsoleColor fg, ConsoleColor bg) => (ushort)((int)fg | ((int)bg<<4));
}