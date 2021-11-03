using System;

namespace ConsoleEngine.Infrastructure.Rendering
{
    public struct Pixel
    {
        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }
        public char Char { get; set; }


        public static readonly Pixel Blank = new() { ForegroundColor = ConsoleColor.Black, BackgroundColor = ConsoleColor.Black, Char = ' ' };
    }
}