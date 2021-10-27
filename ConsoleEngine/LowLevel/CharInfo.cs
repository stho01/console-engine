using System;
using System.Runtime.InteropServices;

namespace ConsoleEngine.LowLevel
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct CharInfo
    {
        [FieldOffset(0)]
        public char UnicodeChar;
        [FieldOffset(0)]
        public char AsciiChar;
        [FieldOffset(2)] //2 bytes seems to work properly
        public UInt16 Attributes;
    }
}