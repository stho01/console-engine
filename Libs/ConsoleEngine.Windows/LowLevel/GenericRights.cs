using System;

namespace ConsoleEngine.LowLevel
{
    [Flags]
    internal enum GenericRights : ulong
    {
        GENERIC_ALL = 0x10000000L,
        GENERIC_EXECUTE = 0x20000000L,
        GENERIC_WRITE = 0x40000000L,
        GENERIC_READ = 0x80000000L
    }
}