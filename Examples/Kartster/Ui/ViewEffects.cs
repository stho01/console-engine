using System;
using ConsoleEngine.Rendering3D;

namespace Kartster.Ui;

/// <summary>
/// What a power-up does to the player's picture, on the finished 3D frame before the UI is drawn over it (Kartster's
/// <c>ViewportPostProcessor</c>, which also leaves the HUD alone): the glitch's horizontal flip and the smoke cloud's
/// dark wash (<see cref="SmokeTint"/>). Both in place, no allocation.
/// </summary>
public static class ViewEffects
{
    /// <summary>Kartster's <c>SmokeTint</c> (28, 28, 32), which the picture is lerped towards by the cover.</summary>
    public const uint SmokeTint = 0x1C1C20;

    /// <summary>Mirrors every row of a <paramref name="width"/>-wide frame left to right.</summary>
    public static void Mirror<T>(Span<T> frame, int width)
    {
        for (var row = 0; row + width <= frame.Length; row += width)
            frame.Slice(row, width).Reverse();
    }

    /// <summary>Lerps every pixel of a 24-bit frame towards <see cref="SmokeTint"/> by <paramref name="cover"/> (0..1).</summary>
    public static void Wash(Span<uint> rgb, float cover)
    {
        var keep = (int)(Math.Clamp(1f - cover, 0f, 1f) * 256f);
        var tint = Scale(SmokeTint, 256 - keep);
        for (var i = 0; i < rgb.Length; i++)
            rgb[i] = Scale(rgb[i], keep) + tint;
    }

    /// <summary>
    /// The 16-colour <see cref="Wash"/>: each palette colour becomes the one nearest its washed shade, through
    /// <paramref name="lookup"/> (room for the palette's colours, filled here).
    /// </summary>
    public static void Wash(Span<ConsoleColor> colors, ConsolePalette palette, float cover, ConsoleColor[] lookup)
    {
        var keep = (int)(Math.Clamp(1f - cover, 0f, 1f) * 256f);
        var tint = Scale(SmokeTint, 256 - keep);
        var paletteColors = palette.Colors;
        for (var i = 0; i < paletteColors.Length; i++)
            lookup[i] = PaletteTarget.Nearest(paletteColors, Scale(paletteColors[i], keep) + tint);
        for (var i = 0; i < colors.Length; i++)
            colors[i] = lookup[(int)colors[i]];
    }

    /// <summary>Each channel of <paramref name="rgb"/> × <paramref name="share"/> / 256.</summary>
    private static uint Scale(uint rgb, int share) =>
        ((((rgb >> 16) & 0xFF) * (uint)share >> 8) << 16) | ((((rgb >> 8) & 0xFF) * (uint)share >> 8) << 8) |
        ((rgb & 0xFF) * (uint)share >> 8);
}
