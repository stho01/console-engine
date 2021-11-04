using System.Runtime.InteropServices;

namespace ConsoleEngine.Native.LowLevel
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ConsoleCursorInfo
    {
        public uint Size;
        public bool Visible;
    }
}