using System;
using System.Runtime.InteropServices;

namespace ConsoleEngine.Native.LowLevel;

[StructLayout(LayoutKind.Explicit, CharSet=CharSet.Unicode)]
internal struct CharInfo
{
    [FieldOffset(0)]
    public char UnicodeChar;
    [FieldOffset(0)]
    public char AsciiChar;
    [FieldOffset(2)] //2 bytes seems to work properly
    public UInt16 Attributes;
}