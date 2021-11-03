using System;

namespace ConsoleEngine.Infrastructure
{
    public struct Pixel
    {
        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }
        public char Char { get; set; }
    }
}