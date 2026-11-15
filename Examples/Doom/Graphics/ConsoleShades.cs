using System;

namespace Doom.Graphics;

/// <summary>
/// Maps the WAD's 256 colours onto the console's 16. Each palette colour becomes the pair of console colours, and
/// the mix of them (0-16 sixteenths), that comes closest to it; <see cref="Pick"/> then chooses one of the two per
/// pixel with a 4 × 4 ordered dither, so areas of a colour show the mix. One table per <c>PLAYPAL</c> palette, so the
/// pain and pickup tints work too. <see cref="Gamma"/> brightens the WAD colours before matching.
/// </summary>
public sealed class ConsoleShades
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The gamma levels F11 cycles through.</summary>
    public static readonly float[] GammaLevels = [1.0f, 1.3f, 1.6f, 2.0f, 2.5f];

    /// <summary>Mix steps between the two colours of a pair.</summary>
    private const int MixSteps = 16;

    /// <summary>How much a far-apart pair is penalised: dithering black with white for grey looks noisy.</summary>
    private const float SpreadPenalty = 0.2f;

    /// <summary>4 × 4 Bayer matrix, thresholds 0-15, indexed <c>(y &amp; 3) * 4 + (x &amp; 3)</c>.</summary>
    private static readonly byte[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>The classic console colours as RGB, in <see cref="ConsoleColor"/> order.</summary>
    private static readonly (byte R, byte G, byte B)[] ConsoleRgb =
    [
        (0, 0, 0), (0, 0, 128), (0, 128, 0), (0, 128, 128), (128, 0, 0), (128, 0, 128), (128, 128, 0),
        (192, 192, 192), (128, 128, 128), (0, 0, 255), (0, 255, 0), (0, 255, 255), (255, 0, 0), (255, 0, 255),
        (255, 255, 0), (255, 255, 255)
    ];

    private readonly Palette _palette;
    private readonly ConsoleColor[] _low;
    private readonly ConsoleColor[] _high;
    private readonly byte[] _mix;

    //**********************************************************
    //** ctor
    //**********************************************************

    public ConsoleShades(Palette palette, int gammaLevel = 1)
    {
        _palette = palette;
        var size = palette.PaletteCount * Palette.ColorCount;
        _low = new ConsoleColor[size];
        _high = new ConsoleColor[size];
        _mix = new byte[size];
        SetGamma(gammaLevel);
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Index into <see cref="GammaLevels"/>.</summary>
    public int GammaLevel { get; private set; }

    public float Gamma => GammaLevels[GammaLevel];

    public int PaletteCount => _palette.PaletteCount;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Rebuilds the tables for gamma level <paramref name="level"/> (wraps around).</summary>
    public void SetGamma(int level)
    {
        GammaLevel = (level % GammaLevels.Length + GammaLevels.Length) % GammaLevels.Length;
        var inverse = 1f / Gamma;
        for (var p = 0; p < _palette.PaletteCount; p++)
        {
            for (var c = 0; c < Palette.ColorCount; c++)
            {
                var (r, g, b) = _palette.GetColor(p, c);
                Match(Brighten(r, inverse), Brighten(g, inverse), Brighten(b, inverse), p * Palette.ColorCount + c);
            }
        }
    }

    /// <summary>The console colour of palette colour <paramref name="color"/> at pixel (x, y), dithered.</summary>
    public ConsoleColor Pick(int palette, byte color, int x, int y)
    {
        var i = palette * Palette.ColorCount + color;
        return _mix[i] > Bayer[((y & 3) << 2) | (x & 3)] ? _high[i] : _low[i];
    }

    /// <summary>The two console colours and the mix (sixteenths of the second) chosen for a palette colour.</summary>
    public (ConsoleColor Low, ConsoleColor High, int Mix) Describe(int palette, byte color)
    {
        var i = palette * Palette.ColorCount + color;
        return (_low[i], _high[i], _mix[i]);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static float Brighten(byte value, float inverseGamma) => 255f * MathF.Pow(value / 255f, inverseGamma);

    /// <summary>
    /// Tries every pair of console colours: the best mix along the line between them is the target's projection onto
    /// that line (weighted RGB), rounded to sixteenths. Keeps the pair with the smallest error.
    /// </summary>
    private void Match(float r, float g, float b, int index)
    {
        const float wr = 2f, wg = 4f, wb = 3f;
        var bestCost = float.MaxValue;
        for (var a = 0; a < ConsoleRgb.Length; a++)
        {
            for (var c = a; c < ConsoleRgb.Length; c++)
            {
                var (ar, ag, ab) = ConsoleRgb[a];
                var (cr, cg, cb) = ConsoleRgb[c];
                float dr = cr - ar, dg = cg - ag, db = cb - ab;
                var length = wr * dr * dr + wg * dg * dg + wb * db * db;
                var t = 0f;
                if (length > 0)
                {
                    t = (wr * (r - ar) * dr + wg * (g - ag) * dg + wb * (b - ab) * db) / length;
                    t = MathF.Round(Math.Clamp(t, 0f, 1f) * MixSteps) / MixSteps;
                }

                var er = ar + dr * t - r;
                var eg = ag + dg * t - g;
                var eb = ab + db * t - b;
                var cost = MathF.Sqrt(wr * er * er + wg * eg * eg + wb * eb * eb)
                           + SpreadPenalty * t * (1 - t) * MathF.Sqrt(length);
                if (cost >= bestCost)
                    continue;

                bestCost = cost;
                _low[index] = (ConsoleColor)a;
                _high[index] = (ConsoleColor)c;
                _mix[index] = (byte)MathF.Round(t * MixSteps);
            }
        }
    }
}
