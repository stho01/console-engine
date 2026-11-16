using System;
using System.IO;
using Doom.Wad;

namespace Doom.Graphics;

/// <summary>
/// The WAD's colours: <c>PLAYPAL</c> (palettes of 256 RGB colours; 0 is normal, 1-8 red pain tints, 9-12 pickup
/// tints, 13 the radiation suit tint) and <c>COLORMAP</c> (light tables: map 0 is full bright, 31 nearly black).
/// </summary>
public sealed class Palette
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int ColorCount = 256;
    public const int LightLevels = 32;
    private const int PaletteSize = ColorCount * 3;

    private readonly byte[] _rgb;
    private readonly byte[] _colorMaps;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Palette(byte[] rgb, byte[] colorMaps)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        ArgumentNullException.ThrowIfNull(colorMaps);
        if (rgb.Length < PaletteSize)
            throw new InvalidDataException(string.Format(DoomTexts.LumpTooShort, "PLAYPAL"));
        if (colorMaps.Length < ColorCount * LightLevels)
            throw new InvalidDataException(string.Format(DoomTexts.LumpTooShort, "COLORMAP"));

        _rgb = rgb;
        _colorMaps = colorMaps;
        PaletteCount = rgb.Length / PaletteSize;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int PaletteCount { get; }

    /// <summary>The light tables, <see cref="LightLevels"/> × 256: <c>ColorMaps[level * 256 + color]</c>.</summary>
    public ReadOnlySpan<byte> ColorMaps => _colorMaps;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static Palette Load(WadFile wad) => new(wad.Read("PLAYPAL").ToArray(), wad.Read("COLORMAP").ToArray());

    public (byte R, byte G, byte B) GetColor(int palette, int color)
    {
        var i = palette * PaletteSize + color * 3;
        return (_rgb[i], _rgb[i + 1], _rgb[i + 2]);
    }

    /// <summary><paramref name="color"/> at light level <paramref name="level"/> (0 brightest).</summary>
    public byte Shade(int level, byte color) => _colorMaps[level * ColorCount + color];

    /// <summary>The palette colour closest to the RGB value, for drawing in a given colour (automap, fallbacks).</summary>
    public byte Closest(byte r, byte g, byte b)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < ColorCount; i++)
        {
            var (pr, pg, pb) = GetColor(0, i);
            var distance = (pr - r) * (pr - r) + (pg - g) * (pg - g) + (pb - b) * (pb - b);
            if (distance >= bestDistance)
                continue;

            best = i;
            bestDistance = distance;
        }

        return (byte)best;
    }
}
