using System;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Physics;
using Topple.Levels;

namespace Topple.Rendering;

/// <summary>
/// Everything that never moves, painted once when a level loads: a dusk sky in bands of colour (dithered where two
/// bands meet), a setting sun, a far mountain range, near hills with trees, and the ground (grass, dirt with pebbles,
/// rock further down, dark pits). Each frame it is copied to the screen in one pass, then the moving parts are drawn
/// over it. Also picks the places of the stars, which twinkle every frame.
/// </summary>
public sealed class Backdrop
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int HudRows = 2;
    public const int StarCount = 50;

    private static readonly SkyBand[] Bands =
    [
        new(HudRows, ConsoleColor.Black),
        new(13, ConsoleColor.DarkBlue),
        new(26, ConsoleColor.DarkMagenta),
        new(35, ConsoleColor.DarkRed)
    ];

    private readonly Pixel[] _pixels;
    private readonly int[] _starX = new int[StarCount];
    private readonly int[] _starY = new int[StarCount];

    //**********************************************************
    //** ctor
    //**********************************************************

    public Backdrop(int width, int height, TileGrid tiles, LevelPlan plan, int seed)
    {
        Width = width;
        Height = height;
        _pixels = new Pixel[width * height];
        PaintSky();
        PaintSun(width - 24, 34, 5.5f);
        PaintRange(far: true, seed);
        PaintRange(far: false, seed);
        PaintGround(tiles, plan, seed);
        PlaceStars(seed);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The sky colour of a row, without dithering.</summary>
    public static ConsoleColor SkyAt(int row)
    {
        var color = Bands[0].Color;
        for (var i = 0; i < Bands.Length; i++)
        {
            if (row >= Bands[i].Top)
                color = Bands[i].Color;
        }

        return color;
    }

    /// <summary>Copies the backdrop to the screen, moved by the canvas' shake (the edge cells repeat).</summary>
    public void DrawTo(Canvas canvas)
    {
        var shake = canvas.Shake;
        for (var y = HudRows; y < Height; y++)
        {
            var sourceY = Math.Clamp(y - shake.Y, HudRows, Height - 1);
            for (var x = 0; x < Width; x++)
            {
                var sourceX = Math.Clamp(x - shake.X, 0, Width - 1);
                ref readonly var pixel = ref _pixels[sourceY * Width + sourceX];
                canvas.Draw(x, y, pixel.Char, pixel.ForegroundColor, pixel.BackgroundColor);
            }
        }
    }

    /// <summary>Stars: mostly dim dots, each one brightening now and then at its own pace.</summary>
    public void DrawStars(Canvas canvas, float seconds)
    {
        for (var i = 0; i < StarCount; i++)
        {
            var phase = seconds * (.7f + (i % 5) * .23f) + i * 1.7f;
            var glow = MathF.Sin(phase);
            var (character, color) = glow switch
            {
                > .93f => ('*', ConsoleColor.White),
                > .6f => ('+', ConsoleColor.Gray),
                > -.4f => ('·', ConsoleColor.Gray),
                _ => ('·', ConsoleColor.DarkGray)
            };
            canvas.DrawWorldOver(_starX[i], _starY[i], character, color);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void PaintSky()
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            Set(x, y, ' ', ConsoleColor.Black, SkyAt(y));

        // Two dithered rows above each band edge, so the colours blend.
        for (var b = 1; b < Bands.Length; b++)
        {
            var above = Bands[b - 1].Color;
            var below = Bands[b].Color;
            for (var x = 0; x < Width; x++)
            {
                Set(x, Bands[b].Top - 2, '░', below, above);
                Set(x, Bands[b].Top - 1, '▒', below, above);
            }
        }
    }

    private void PaintSun(int centerX, int centerY, float radius)
    {
        for (var y = centerY - (int)radius - 3; y <= centerY + (int)radius + 3; y++)
        for (var x = centerX - (int)radius - 3; x <= centerX + (int)radius + 3; x++)
        {
            var distance = MathF.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
            if (distance < radius - 1.5f)
                Set(x, y, '█', ConsoleColor.Yellow, ConsoleColor.Yellow);
            else if (distance < radius)
                Set(x, y, '▓', ConsoleColor.Yellow, ConsoleColor.DarkYellow);
            else if (distance < radius + 2.5f && (x + y) % 2 == 0)
                Set(x, y, '░', ConsoleColor.DarkYellow, SkyAt(y));
        }
    }

    /// <summary>
    /// A mountain range (far: dark blue, snow on the high peaks) or the hills in front of it (near: low, dark green,
    /// with trees).
    /// </summary>
    private void PaintRange(bool far, int seed)
    {
        var shift = (seed % 97) * .37f;
        for (var x = 0; x < Width; x++)
        {
            var at = x + shift;
            var top = far
                ? 35f + 3f * MathF.Sin(at * .09f) + 2f * MathF.Sin(at * .21f + 1.3f) + 1.2f * MathF.Sin(at * .43f)
                : 44.5f + 1.5f * MathF.Sin(at * .06f + 2f) + MathF.Sin(at * .17f + .7f);
            var row = (int)MathF.Floor(top);
            var color = far ? ConsoleColor.DarkBlue : ConsoleColor.DarkGreen;
            for (var y = row + 1; y < Height; y++)
                Set(x, y, ' ', color, color);

            if (far)
            {
                // Snow on the peaks, a half block where the slope ends halfway down a cell.
                var snowy = row < 33;
                Set(x, row, top - row < .5f ? '▄' : '▀', snowy ? ConsoleColor.Gray : color, SkyAt(row));
                if (snowy)
                    Set(x, row + 1, '▀', ConsoleColor.Gray, color);
            }
            else
            {
                // Tree tops: a full or half block above the hill line, some two cells high.
                var tree = Noise.Unit(x, 0, seed + 31);
                Set(x, row, tree < .45f ? '█' : '▄', color, SkyAt(row));
                if (tree < .2f)
                    Set(x, row - 1, '▄', color, SkyAt(row - 1));
            }
        }
    }

    private void PaintGround(TileGrid tiles, LevelPlan plan, int seed)
    {
        for (var x = 0; x < Width; x++)
        {
            var depth = -1;
            for (var y = HudRows; y < Height; y++)
            {
                if (!tiles.IsSolid(x, y))
                {
                    depth = -1;
                    continue;
                }

                // The side walls stay invisible above the ground next to them: the screen edge is the wall.
                var edge = x == 0 || x == Width - 1;
                if (edge && !tiles.IsSolid(x == 0 ? 1 : x - 1, y))
                    continue;

                depth++;
                if (depth == 0 && !edge && !IsInPit(plan, x, y - 1))
                    PaintGrassTop(x, y, seed);
                else
                    PaintEarth(x, y, edge ? 99 : depth + (depth == 0 ? 1 : 0), seed);
            }
        }

        foreach (var pit in plan.Pits)
        {
            var water = LevelBuilder.WaterOf(pit);
            for (var y = pit.Top; y < water.Top; y++)
            for (var x = pit.Left; x <= pit.Right; x++)
                Set(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        }
    }

    private static bool IsInPit(LevelPlan plan, int x, int y)
    {
        foreach (var pit in plan.Pits)
        {
            if (x >= pit.Left && x <= pit.Right && y >= pit.Top && y <= pit.Bottom)
                return true;
        }

        return false;
    }

    private void PaintGrassTop(int x, int y, int seed)
    {
        var dark = Noise.Unit(x, y, seed) < .2f;
        Set(x, y, '▀', dark ? ConsoleColor.DarkGreen : ConsoleColor.Green, ConsoleColor.DarkYellow);

        // Tufts and flowers in the air cell above.
        var tuft = Noise.Unit(x, y, seed + 1);
        var above = y - 1;
        var sky = Get(x, above).BackgroundColor;
        if (tuft < .25f)
            Set(x, above, tuft < .08f ? '"' : ',', ConsoleColor.Green, sky);
        else if (tuft < .29f)
            Set(x, above, '*', tuft < .27f ? ConsoleColor.Yellow : ConsoleColor.Red, sky);
    }

    private void PaintEarth(int x, int y, int depth, int seed)
    {
        var grain = Noise.Unit(x, y, seed + 2);
        if (depth <= 3)
        {
            var (character, color) = grain switch
            {
                < .08f => ('°', ConsoleColor.Yellow),
                < .16f => ('.', ConsoleColor.DarkGray),
                < .2f => ('∙', ConsoleColor.Black),
                _ => (' ', ConsoleColor.DarkYellow)
            };
            Set(x, y, character, color, ConsoleColor.DarkYellow);
        }
        else if (depth == 4)
        {
            Set(x, y, grain < .5f ? '▒' : '░', ConsoleColor.DarkGray, ConsoleColor.DarkYellow);
        }
        else
        {
            var (character, color) = grain switch
            {
                < .1f => ('▓', ConsoleColor.Gray),
                < .25f => ('░', ConsoleColor.Black),
                _ => (' ', ConsoleColor.DarkGray)
            };
            Set(x, y, character, color, ConsoleColor.DarkGray);
        }
    }

    private void PlaceStars(int seed)
    {
        for (int i = 0, attempt = 0; i < StarCount; attempt++)
        {
            var x = (int)(Noise.Unit(attempt, 0, seed + 5) * Width);
            var y = HudRows + (int)(Noise.Unit(attempt, 1, seed + 5) * 22);
            var pixel = Get(x, y);
            var isSky = pixel.Char == ' ' && pixel.BackgroundColor is ConsoleColor.Black or ConsoleColor.DarkBlue;
            if (!isSky && attempt < 10_000)
                continue;

            _starX[i] = x;
            _starY[i] = y;
            i++;
        }
    }

    private Pixel Get(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _pixels[y * Width + x] : Pixel.Blank;

    private void Set(int x, int y, char character, ConsoleColor foreground, ConsoleColor background)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        _pixels[y * Width + x] = new Pixel
        {
            Char = character,
            ForegroundColor = foreground,
            BackgroundColor = background
        };
    }

    private readonly record struct SkyBand(int Top, ConsoleColor Color);
}
