using System;
using System.Runtime.InteropServices;
using System.Security;

namespace ConsoleEngine.Native.LowLevel
{
    /// <summary>
    /// https://www.pinvoke.net/
    /// https://www.pinvoke.net/default.aspx/kernel32/ConsoleFunctions.html
    /// </summary>
    internal static class Kernel32
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetStdHandle(StdHandle nStdHandle);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetConsoleActiveScreenBuffer(IntPtr hConsoleOutput);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateConsoleScreenBuffer(GenericRights dwDesiredAccess, uint dwShareMode, IntPtr securityAttributes, uint flags, IntPtr screenBufferData);
        
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool WriteConsoleOutputCharacter(IntPtr screenBuffer,string characters,uint length,Coord writeCoord, out UInt32 numCharsWritten );

        /// <summary> Writes the char information buffer to the console.</summary>
        /// <param name="hConsoleOutput"></param>
        /// <param name="lpBuffer">The char info buffer that is written to the console</param>
        /// <param name="dwBufferSize">Size of the char info buffer</param>
        /// <param name="dwBufferCoord">Where in the lpBuffer we should start drawing from</param>
        /// <param name="lpWriteRegion">Where on the screen/console we should start drawing to</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", EntryPoint = "WriteConsoleOutputW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool WriteConsoleOutput(IntPtr hConsoleOutput, [MarshalAs(UnmanagedType.LPArray), In] CharInfo[] lpBuffer, Coord dwBufferSize, Coord dwBufferCoord, ref SmallRect lpWriteRegion);

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetConsoleWindow();
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern Int32 SetCurrentConsoleFontEx(IntPtr consoleOutput, bool maximumWindow, ref ConsoleFontInfoEx consoleCurrentFontEx);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetConsoleWindowInfo(IntPtr hConsoleOutput, bool bAbsolute, ref SmallRect lpConsoleWindow);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetConsoleScreenBufferSize(IntPtr hConsoleOutput, Coord dwSize);
        
        [DllImport("user32.dll")]
        internal static extern int GetAsyncKeyState(int vKeys);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetConsoleCP(uint wCodePageID);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetConsoleTitle(string lpConsoleTitle);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetConsoleCursorInfo(IntPtr hConsoleOutput, out ConsoleCursorInfo lpConsoleCursorInfo);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetConsoleCursorInfo(IntPtr hConsoleOutput, [In] ref ConsoleCursorInfo lpConsoleCursorInfo);
        
        [DllImport("kernel32.dll", SetLastError=true)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);
    }
}