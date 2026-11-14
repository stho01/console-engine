namespace Kartster.Ui;

/// <summary>Draws into a true-colour frame (<c>IRenderer3D.Rgb</c>, <c>0xRRGGBB</c> per pixel) as is.</summary>
public sealed class RgbTarget(uint[] frame, int width, int height) : IPixelTarget
{
    /// <summary>The frame drawn into, <c>index = y * Width + x</c>. Swap it when the renderer changes.</summary>
    public uint[] Frame { get; set; } = frame;

    public int Width { get; } = width;
    public int Height { get; } = height;

    public void Set(int x, int y, uint rgb)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            Frame[y * Width + x] = rgb & 0xFFFFFF;
    }
}
