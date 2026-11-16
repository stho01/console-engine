namespace Doom.Graphics;

/// <summary>
/// A wall texture composed from patches (<c>TEXTURE1</c>/<c>TEXTURE2</c>), stored column by column
/// (<c>x * Height + y</c>). Pixels no patch covers are see-through, which only matters on two-sided lines.
/// </summary>
public sealed class Texture(string name, int width, int height)
{
    //**********************************************************
    //** props
    //**********************************************************

    public string Name { get; } = name;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public byte[] Pixels { get; } = new byte[width * height];
    public bool[] Opaque { get; } = new bool[width * height];
}

/// <summary>A 64 × 64 floor or ceiling picture, stored row by row (<c>y * 64 + x</c>).</summary>
public sealed class Flat(string name, byte[] pixels)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Size = 64;
    public const int Mask = Size - 1;

    //**********************************************************
    //** props
    //**********************************************************

    public string Name { get; } = name;
    public byte[] Pixels { get; } = pixels;
}
