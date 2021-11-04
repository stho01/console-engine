namespace ConsoleEngine.Native.LowLevel
{
    internal struct SmallRect
    {
        public short Left { get; }
        public short Top { get; }
        public short Right { get; }
        public short Bottom { get; }

        public SmallRect(short left, short top, short right, short bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }
}