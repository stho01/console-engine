using System.Runtime.InteropServices;

namespace ConsoleEngine.Native.LowLevel;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ConsoleFontInfoEx
{
    public uint cbSize;
    public uint nFont;
    public Coord dwFontSize;
    public int FontFamily;
    public int FontWeight;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] // Edit sizeconst if the font name is too big
    public string FaceName;
}