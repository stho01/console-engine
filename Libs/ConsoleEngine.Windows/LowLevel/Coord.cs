using System.Runtime.InteropServices;

namespace ConsoleEngine.Native.LowLevel
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Coord
    {
        public short X;
        public short Y;

        public Coord(short x, short y)
        {
            X = x;
            Y = y;
        }

        public static readonly Coord Zero = new(0, 0);
    }
}