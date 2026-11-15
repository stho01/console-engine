namespace Kartster.Ui;

/// <summary>
/// What the UI draws into: one colour per pixel, <c>0xRRGGBB</c>. A pixel outside Width × Height is ignored, so text
/// and shapes clip at the edges. <see cref="PaletteTarget"/> (16 console colours) and <see cref="RgbTarget"/>
/// (24-bit) are the two frames the game has.
/// </summary>
public interface IPixelTarget
{
    int Width { get; }
    int Height { get; }

    void Set(int x, int y, uint rgb);
}
