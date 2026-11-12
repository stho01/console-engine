using System;

namespace Vikingferd.Rendering;

/// <summary>
/// The night behind every level and the title: a sky that gets bluer towards the horizon, the moon, snow-capped
/// mountains and a pine forest (all built once), with a moving aurora, twinkling stars and falling snow on top. Tiles
/// and heroes are drawn over it.
/// </summary>
public sealed class Backdrop
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int FlakeCount = 70;
    private const int AuroraTop = 8;
    private const char BrightStar = '∙';
    private const char DimStar = '·';

    private readonly int _width;
    private readonly int _pixelHeight;
    private readonly ConsoleColor[] _pixels;

    /// <summary>Per pixel: open sky (stars and aurora may be drawn there).</summary>
    private readonly bool[] _sky;

    private readonly Flake[] _flakes = new Flake[FlakeCount];
    private readonly Random _random;
    private float _time;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Backdrop(int width, int pixelHeight, int seed = 7)
    {
        _width = width;
        _pixelHeight = pixelHeight;
        _pixels = new ConsoleColor[width * pixelHeight];
        _sky = new bool[width * pixelHeight];
        _random = new Random(seed);
        Build();
        for (var i = 0; i < _flakes.Length; i++)
            _flakes[i] = NewFlake(_random.NextSingle() * pixelHeight);
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        for (var i = 0; i < _flakes.Length; i++)
        {
            ref var flake = ref _flakes[i];
            flake.Y += flake.Speed * deltaSeconds;
            flake.X += MathF.Sin(_time * 1.3f + flake.Phase) * 2.5f * deltaSeconds;
            if (flake.Y >= _pixelHeight)
                flake = NewFlake(0f);
        }
    }

    public void Draw(Canvas canvas)
    {
        canvas.Fill(_pixels);
        DrawAurora(canvas);
        DrawStars(canvas);
        for (var i = 0; i < _flakes.Length; i++)
        {
            ref readonly var flake = ref _flakes[i];
            canvas.Pixel((int)flake.X, (int)flake.Y, flake.Speed > 9f ? ConsoleColor.White : ConsoleColor.Gray);
        }
    }

    //**********************************************************
    //** internals: built once
    //**********************************************************

    private void Build()
    {
        for (var x = 0; x < _width; x++)
        {
            var ridge = MountainRidge(x);
            var forest = ForestEdge(x);
            for (var y = 0; y < _pixelHeight; y++)
            {
                var i = y * _width + x;
                if (y >= forest)
                    _pixels[i] = ConsoleColor.Black;
                else if (y >= forest - TreeHeight(x, forest - y))
                    _pixels[i] = ConsoleColor.DarkGreen;
                else if (y >= ridge)
                    _pixels[i] = y - ridge < SnowDepth(ridge, x) ? Snowcap(x, y) : MountainColor(x, y, y - ridge);
                else
                {
                    _pixels[i] = SkyColor(x, y);
                    _sky[i] = true;
                }
            }
        }

        DrawMoon(_width - 18, 9);
    }

    private static float Peak(float t)
    {
        var fraction = t - MathF.Floor(t);
        return 1f - MathF.Abs(2f * fraction - 1f);
    }

    private float MountainRidge(int x) =>
        _pixelHeight * .62f - 20f * Peak(x / 43f + .2f) - 9f * Peak(x / 17f + .65f) - 3f * Peak(x / 7f);

    private float ForestEdge(int x) => _pixelHeight * .8f - 3f * Peak(x / 29f);

    /// <summary>How far above the forest edge a pine reaches at this column, or 0 between the trees.</summary>
    private static int TreeHeight(int x, float above)
    {
        var tree = x / 5;
        var offset = x % 5;
        var height = 4 + Looks.Hash(tree, 3) % 6;
        var halfWidth = (int)(2f * (1f - above / height) + .5f);
        return Math.Abs(offset - 2) <= halfWidth && above <= height ? height : 0;
    }

    /// <summary>Snow only on the high parts of the range, deeper the higher the ridge.</summary>
    private float SnowDepth(float ridge, int x)
    {
        var line = _pixelHeight * .45f;
        return ridge < line ? 1f + (line - ridge) * .35f + Looks.Hash(x, 5) % 2 : 0f;
    }

    private static ConsoleColor Snowcap(int x, int y) =>
        Looks.Hash(x, y, 9) < 170 ? ConsoleColor.Gray : ConsoleColor.DarkGray;

    /// <summary>
    /// The console's dark colours are strong, so the night is black with a hint of blue near the horizon, and the
    /// mountains are black with a blue rim and sparse blue speckles that thin out downwards.
    /// </summary>
    private ConsoleColor SkyColor(int x, int y)
    {
        var horizon = _pixelHeight * .55f;
        var share = MathF.Max(0f, 1f - (horizon - y) / 14f) * .12f;
        return Looks.Hash(x, y, 1) < share * 255f ? ConsoleColor.DarkBlue : ConsoleColor.Black;
    }

    private static ConsoleColor MountainColor(int x, int y, float depth)
    {
        if (depth < 1f)
            return ConsoleColor.DarkBlue;

        var share = MathF.Max(0f, .2f - depth * .02f);
        return Looks.Hash(x, y, 2) < share * 255f ? ConsoleColor.DarkBlue : ConsoleColor.Black;
    }

    private void DrawMoon(int centerX, int centerY)
    {
        const int radius = 4;
        for (var y = -radius; y <= radius; y++)
        for (var x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > radius * radius + 1)
                continue;

            var i = (centerY + y) * _width + centerX + x;
            var crater = (x == 1 && y == -1) || (x == -2 && y == 1) || (x == 0 && y == 2);
            _pixels[i] = crater ? ConsoleColor.Gray : ConsoleColor.White;
            _sky[i] = false;
        }
    }

    //**********************************************************
    //** internals: animated
    //**********************************************************

    /// <summary>
    /// Curtains of light: from a slowly waving top edge, vertical rays hang down, brightest at the top, longest where
    /// the curtain is strongest, fading out in a dither at the bottom.
    /// </summary>
    private void DrawAurora(Canvas canvas)
    {
        for (var x = 0; x < _width; x++)
        {
            var strength = .5f + .3f * MathF.Sin(x * .045f + _time * .7f) + .2f * MathF.Sin(x * .13f - _time * 1.3f);
            if (strength < .35f)
                continue;

            var top = (int)(AuroraTop + 3f * MathF.Sin(x * .06f + _time * .3f) + 1.5f * MathF.Sin(x * .17f - _time * .5f));
            var length = (int)(3f + 10f * (strength - .35f));
            var shimmer = (int)(_time * 3f);
            for (var k = 0; k <= length; k++)
            {
                var y = top + k;
                if (y < 0 || !_sky[y * _width + x])
                    continue;

                // Sparse, flickering rays: densest just under the top edge, thinning towards the bottom.
                var density = (1f - (float)k / (length + 1)) * strength * .75f;
                var hash = Looks.Hash(x, y, shimmer);
                if (hash >= density * 255f)
                    continue;

                var color = k <= 1 && hash < 40 ? ConsoleColor.Green
                    : k > length * .6f ? ConsoleColor.DarkCyan
                    : ConsoleColor.DarkGreen;
                canvas.Pixel(x, y, color);
            }
        }
    }

    private void DrawStars(Canvas canvas)
    {
        var twinkle = (int)(_time * 2.5f);
        for (var row = 0; row < _pixelHeight / 2; row++)
        for (var x = 0; x < _width; x++)
        {
            if (Looks.Hash(x, row, 21) >= 5 || !_sky[row * 2 * _width + x] || !_sky[(row * 2 + 1) * _width + x])
                continue;

            var top = canvas.PixelAt(x, row * 2);
            if (top != ConsoleColor.Black && top != ConsoleColor.DarkBlue)
                continue;

            var shine = Looks.Hash(x, row, twinkle);
            if (shine < 50)
                canvas.Glyph(x, row, BrightStar, ConsoleColor.White);
            else if (shine < 170)
                canvas.Glyph(x, row, DimStar, ConsoleColor.Gray);
            else
                canvas.Glyph(x, row, DimStar, ConsoleColor.DarkGray);
        }
    }

    private Flake NewFlake(float y) => new()
    {
        X = _random.NextSingle() * _width,
        Y = y,
        Speed = 5f + _random.NextSingle() * 8f,
        Phase = _random.NextSingle() * 6.3f
    };

    private struct Flake
    {
        public float X;
        public float Y;

        /// <summary>Pixels per second. Fast flakes are near and drawn white, slow ones grey.</summary>
        public float Speed;

        public float Phase;
    }
}
