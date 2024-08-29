namespace ConsoleEngine.Native.LowLevel;

internal struct SmallRect(short left, short top, short right, short bottom)
{
    public short Left { get; } = left;
    public short Top { get; } = top;
    public short Right { get; } = right;
    public short Bottom { get; } = bottom;
}