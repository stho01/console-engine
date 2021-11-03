using System.Runtime.InteropServices;

namespace ConsoleEngine.LowLevel
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ConsoleCursorInfo
    {
        public uint Size;
        public bool Visible;
    }
}