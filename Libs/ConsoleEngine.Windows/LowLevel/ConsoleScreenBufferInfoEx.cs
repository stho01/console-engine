using System.Runtime.InteropServices;

namespace ConsoleEngine.Native.LowLevel;

/// <summary>CONSOLE_SCREEN_BUFFER_INFOEX. <see cref="ColorTable"/> holds COLORREFs: <c>0x00BBGGRR</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ConsoleScreenBufferInfoEx
{
    public uint cbSize;
    public Coord dwSize;
    public Coord dwCursorPosition;
    public ushort wAttributes;
    public SmallRect srWindow;
    public Coord dwMaximumWindowSize;
    public ushort wPopupAttributes;
    [MarshalAs(UnmanagedType.Bool)]
    public bool bFullscreenSupported;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public uint[] ColorTable;
}
