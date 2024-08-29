using System.Runtime.InteropServices;

namespace ConsoleEngine.Native.LowLevel;

[StructLayout(LayoutKind.Sequential)]
internal struct Coord(short x, short y)
{
    public short X = x;
    public short Y = y;

    public static readonly Coord Zero = new(0, 0);
}