using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// Software triangle rasterizer: transforms a <see cref="Mesh"/>, clips it at the near plane, culls back faces,
/// lights each triangle flat
/// (ambient + one directional light) and fills it into <see cref="Colors"/> with a depth test. Local lights
/// (<see cref="Lighting"/>) are added per pixel, at each pixel's perspective-correct world position, but only on the
/// triangles they can reach (bounding sphere, front side); elsewhere they would add exactly nothing.
/// Allocates nothing per frame once its vertex buffer has grown to the largest mesh.
/// In <see cref="TrueColor"/> mode it fills a float RGB buffer instead (no dither), packed into <see cref="Rgb"/> by
/// <see cref="Finish"/>. A mesh's opaque triangles are drawn first, then its see-through ones, blended
/// <c>dst = lerp(dst, src, opacity)</c>, depth-tested but not writing depth.
/// </summary>
public sealed class Rasterizer : IRenderer3D
{
    //**********************************************************
    //** fields
    //**********************************************************

    internal const string PaletteClearInTrueColor =
        "This renderer is in true-colour mode: call Clear(ReadOnlySpan<uint>) with a 0xRRGGBB background.";
    internal const string TrueColorClearInPaletteMode =
        "This renderer is in palette mode: call Clear(ReadOnlySpan<ConsoleColor>), or create it with trueColor: true.";

    private readonly ConsolePalette _palette;
    private readonly float[] _depth;
    private readonly Vector3[] _shaded; // true colour: the frame as 0..1 RGB until Finish packs it
    private float _opacity = 1f; // of the triangle being filled
    private int _lightMask; // of the triangle being filled: bit i set = Lighting.Lights[i] can reach it
    private Vector3 _albedo; // of the triangle being filled, while _lightMask != 0
    private Vector3 _normal; // of the triangle being filled, world space, while _lightMask != 0

    // Lights sorted into 8×8-pixel screen tiles (bit i: light i's bounds cover the tile), so a pixel only evaluates
    // the lights that can reach it. Rebuilt when the lights or the camera change.
    private const int TileShift = 3;
    private readonly int _tilesX;
    private readonly int[] _tileLights;
    private readonly Vector3[] _boundCenters = new Vector3[SceneLighting.MaxLights];
    private readonly float[] _boundRadii = new float[SceneLighting.MaxLights];
    private SceneLighting _tiledLighting;
    private int _tiledVersion;
    private Matrix4x4 _tiledViewProjection;

    /// <summary>
    /// Screen-door transparency: a pixel of a triangle with opacity <c>o</c> is drawn where this 4×4 ordered
    /// threshold is below <c>o</c>, so <c>o</c> of the pixels show and the rest let the scene behind through.
    /// </summary>
    private static readonly float[] ScreenDoor = BuildScreenDoor();
    private Vector4[] _clip = [];
    private Corner[] _corners = [];

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="palette">The console colours of palette mode; not used when <paramref name="trueColor"/>.</param>
    /// <param name="trueColor">Draw 24-bit colour into <see cref="Rgb"/> instead of palette indices.</param>
    public Rasterizer(int width, int height, ConsolePalette palette, bool trueColor = false)
    {
        _palette = palette;
        Width = width;
        Height = height;
        TrueColor = trueColor;
        Colors = trueColor ? [] : new ConsoleColor[width * height];
        Rgb = trueColor ? new uint[width * height] : [];
        _shaded = trueColor ? new Vector3[width * height] : [];
        _depth = new float[width * height];
        _tilesX = (width >> TileShift) + 1;
        _tileLights = new int[_tilesX * ((height >> TileShift) + 1)];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }
    public bool TrueColor { get; }

    /// <summary>The palette-mode frame, one colour per pixel, <c>index = y * Width + x</c>. Empty in true colour.</summary>
    public ConsoleColor[] Colors { get; }

    /// <summary>The true-colour frame, <c>0xRRGGBB</c>, after <see cref="Finish"/>. Empty in palette mode.</summary>
    public uint[] Rgb { get; }

    /// <inheritdoc/>
    public SceneLighting Lighting { get; set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Starts a frame: copies the background into <see cref="Colors"/> and resets the depth buffer.</summary>
    public void Clear(ReadOnlySpan<ConsoleColor> background)
    {
        if (TrueColor)
            throw new InvalidOperationException(PaletteClearInTrueColor);

        background.CopyTo(Colors);
        Array.Fill(_depth, 1f);
        Lighting = null;
    }

    /// <summary>Starts a true-colour frame on <paramref name="backgroundRgb"/> (<c>0xRRGGBB</c> per pixel).</summary>
    public void Clear(ReadOnlySpan<uint> backgroundRgb)
    {
        if (!TrueColor)
            throw new InvalidOperationException(TrueColorClearInPaletteMode);

        for (var i = 0; i < _shaded.Length; i++)
        {
            var rgb = backgroundRgb[i];
            _shaded[i] = new Vector3((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF) / 255f;
        }

        Array.Fill(_depth, 1f);
        Lighting = null;
    }

    /// <param name="toLight">Unit vector from the scene towards the sun, in world space.</param>
    public void Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection, Vector3 toLight)
    {
        var positions = mesh.Positions;
        if (_clip.Length < positions.Length)
        {
            _clip = new Vector4[positions.Length];
            _corners = new Corner[positions.Length];
        }

        var lit = !mesh.IsUnlit && Lighting != null && Lighting.LightCount > 0;
        if (lit && (Lighting != _tiledLighting || Lighting.Version != _tiledVersion
                                                 || viewProjection != _tiledViewProjection))
            TileLights(viewProjection);

        var worldViewProjection = world * viewProjection;
        for (var i = 0; i < positions.Length; i++)
        {
            _clip[i] = Vector4.Transform(positions[i], worldViewProjection);
            // garbage behind the near plane, those corners are clipped first
            _corners[i] = ToCorner(_clip[i], lit ? Vector3.Transform(positions[i], world) : default);
        }

        // True colour: opaque triangles first, then the blended ones over them (palette mode: one pass, in order).
        var passes = TrueColor && mesh.TriangleOpacity != null ? 2 : 1;
        for (var pass = 0; pass < passes; pass++)
            DrawTriangles(mesh, world, toLight, lit, blendedPass: pass == 1);
    }

    /// <summary>Packs the true-colour frame into <see cref="Rgb"/>; nothing to do in palette mode.</summary>
    public void Finish()
    {
        for (var i = 0; i < _shaded.Length; i++)
        {
            var rgb = _shaded[i] * 255f + new Vector3(0.5f);
            Rgb[i] = ((uint)rgb.X << 16) | ((uint)rgb.Y << 8) | (uint)rgb.Z;
        }
    }

    public void Dispose()
    {
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawTriangles(Mesh mesh, Matrix4x4 world, Vector3 toLight, bool lit, bool blendedPass)
    {
        var ambient = Lighting?.Ambient ?? SceneLighting.DefaultAmbient;
        var sunColor = Lighting?.SunColor ?? SceneLighting.DefaultSunColor;
        var indices = mesh.Indices;
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var ia = indices[t * 3];
            var ib = indices[t * 3 + 1];
            var ic = indices[t * 3 + 2];
            _opacity = mesh.TriangleOpacity == null ? 1f : mesh.TriangleOpacity[t];
            if (_opacity <= 0f || (TrueColor && (_opacity < 1f) != blendedPass))
                continue;

            var color = mesh.TriangleColors[t];
            _lightMask = 0;
            if (!mesh.IsUnlit)
            {
                var normal = Vector3.Normalize(Vector3.TransformNormal(mesh.TriangleNormals[t], world));
                if (lit)
                {
                    _lightMask = LightsReaching(_corners[ia].World, _corners[ib].World, _corners[ic].World, normal);
                    _albedo = color;
                    _normal = normal;
                }

                color *= ambient + sunColor * MathF.Max(0, Vector3.Dot(normal, toLight));
            }

            // z < 0 is in front of the near plane (System.Numerics projections map near..far to 0..1).
            var behind = (_clip[ia].Z < 0 ? 1 : 0) + (_clip[ib].Z < 0 ? 1 : 0) + (_clip[ic].Z < 0 ? 1 : 0);
            if (behind == 0)
                FillIfFrontFacing(_corners[ia], _corners[ib], _corners[ic], color);
            else if (behind < 3)
                ClipAndFill(ia, ib, ic, color);
        }
    }

    /// <summary>
    /// Which lights can reach the triangle a, b, c (world space): bit i for <c>Lighting.Lights[i]</c> when it is on
    /// the triangle's front side and its radius touches the triangle's bounding sphere.
    /// </summary>
    private int LightsReaching(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
    {
        var center = (a + b + c) / 3f;
        var reach = MathF.Sqrt(MathF.Max(Vector3.DistanceSquared(a, center),
            MathF.Max(Vector3.DistanceSquared(b, center), Vector3.DistanceSquared(c, center))));
        var lights = Lighting.Lights;
        var mask = 0;
        for (var i = 0; i < lights.Length; i++)
        {
            var touch = _boundRadii[i] + reach;
            if (Vector3.Dot(normal, lights[i].Position - a) > 0
                && Vector3.DistanceSquared(_boundCenters[i], center) < touch * touch)
                mask |= 1 << i;
        }

        return mask;
    }

    /// <summary>
    /// Sorts the lights into screen tiles for <paramref name="viewProjection"/>: each light's bounding sphere
    /// (<see cref="SceneLighting.Bounds"/>), projected through its box's corners, or the whole screen when the box
    /// reaches the near plane.
    /// </summary>
    private void TileLights(Matrix4x4 viewProjection)
    {
        _tiledLighting = Lighting;
        _tiledVersion = Lighting.Version;
        _tiledViewProjection = viewProjection;
        Array.Clear(_tileLights);

        var lights = Lighting.Lights;
        for (var i = 0; i < lights.Length; i++)
        {
            var (center, radius) = SceneLighting.Bounds(lights[i]);
            _boundCenters[i] = center;
            _boundRadii[i] = radius;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var wholeScreen = false;
            for (var corner = 0; corner < 8 && !wholeScreen; corner++)
            {
                var offset = new Vector3((corner & 1) == 0 ? -radius : radius, (corner & 2) == 0 ? -radius : radius,
                    (corner & 4) == 0 ? -radius : radius);
                var clip = Vector4.Transform(center + offset, viewProjection);
                wholeScreen = clip.Z < 0;
                var screen = ToScreen(clip);
                minX = MathF.Min(minX, screen.X);
                minY = MathF.Min(minY, screen.Y);
                maxX = MathF.Max(maxX, screen.X);
                maxY = MathF.Max(maxY, screen.Y);
            }

            var x0 = wholeScreen ? 0 : Math.Max(0, (int)MathF.Floor(minX)) >> TileShift;
            var y0 = wholeScreen ? 0 : Math.Max(0, (int)MathF.Floor(minY)) >> TileShift;
            var x1 = wholeScreen ? _tilesX - 1 : Math.Min(Width - 1, (int)MathF.Ceiling(maxX)) >> TileShift;
            var y1 = wholeScreen
                ? _tileLights.Length / _tilesX - 1
                : Math.Min(Height - 1, (int)MathF.Ceiling(maxY)) >> TileShift;
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                _tileLights[y * _tilesX + x] |= 1 << i;
        }
    }

    /// <summary>The flat colour plus what the lights in <paramref name="mask"/> add at world position
    /// <paramref name="position"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // keeps the unlit pixel loop small
    private Vector3 AddLights(Vector3 flat, int mask, Vector3 position)
    {
        // SceneLighting.Contribution, leaving each light as soon as it is known to add nothing.
        var lights = Lighting.Lights;
        var sum = Vector3.Zero;
        for (; mask != 0; mask &= mask - 1)
        {
            ref readonly var light = ref lights[BitOperations.TrailingZeroCount(mask)];
            var toLight = light.Position - position;
            var squared = toLight.LengthSquared();
            var fraction = 1f - squared / (light.Radius * light.Radius);
            if (fraction <= 0f)
                continue;

            // Outside a cone of up to 90° either side (cos ≥ 0), without the square root: along ≤ cos · distance.
            var along = -Vector3.Dot(toLight, light.Direction);
            if (light.ConeCosine >= 0f && (along <= 0f || along * along <= light.ConeCosine * light.ConeCosine * squared))
                continue;

            var inverseDistance = 1f / MathF.Max(MathF.Sqrt(squared), 0.001f);
            var facing = Vector3.Dot(_normal, toLight * inverseDistance);
            var cone = 1f;
            if (light.ConeCosine >= -0.5f)
            {
                var t = (-Vector3.Dot(toLight * inverseDistance, light.Direction) - light.ConeCosine)
                        / PointLight.ConeSoftness;
                if (t <= 0f)
                    continue;
                t = MathF.Min(t, 1f);
                cone = t * t * (3f - 2f * t);
            }

            if (facing > 0f)
                sum += light.Color * (fraction * fraction * MathF.Min(facing, 1f) * cone);
        }

        var color = flat + _albedo * sum;
        return TrueColor ? Vector3.Clamp(color, Vector3.Zero, Vector3.One) : color;
    }

    private Corner ToCorner(Vector4 clip, Vector3 world) =>
        new() { Screen = ToScreen(clip), InverseW = 1f / clip.W, World = world };

    private Vector3 ToScreen(Vector4 clip)
    {
        var inverseW = 1f / clip.W;
        return new Vector3(
            (clip.X * inverseW + 1f) * 0.5f * Width,
            (1f - clip.Y * inverseW) * 0.5f * Height,
            clip.Z * inverseW);
    }

    /// <summary>
    /// Cuts the part of the triangle (corner indices) behind the near plane (z &lt; 0) off. One or two corners behind
    /// leave a triangle or a quad, filled as one or two triangles. Corner order is kept, so the winding stays.
    /// </summary>
    private void ClipAndFill(int a, int b, int c, Vector3 color)
    {
        Span<Corner> kept = stackalloc Corner[4];
        var count = 0;
        ClipEdge(a, b, kept, ref count);
        ClipEdge(b, c, kept, ref count);
        ClipEdge(c, a, kept, ref count);

        for (var i = 2; i < count; i++)
            FillIfFrontFacing(kept[0], kept[i - 1], kept[i], color);
    }

    /// <summary>Keeps corner <paramref name="from"/> if in front, and where the edge crosses the near plane.</summary>
    private void ClipEdge(int from, int to, Span<Corner> kept, ref int count)
    {
        var fromClip = _clip[from];
        var toClip = _clip[to];
        if (fromClip.Z >= 0)
            kept[count++] = _corners[from];
        if ((fromClip.Z < 0) != (toClip.Z < 0))
        {
            var t = fromClip.Z / (fromClip.Z - toClip.Z);
            kept[count++] = ToCorner(Vector4.Lerp(fromClip, toClip, t),
                Vector3.Lerp(_corners[from].World, _corners[to].World, t));
        }
    }

    private void FillIfFrontFacing(in Corner a, in Corner b, in Corner c, Vector3 color)
    {
        // Counter-clockwise front faces turn clockwise (negative area) once screen y points down.
        var area = Edge(a.Screen, b.Screen, c.Screen.X, c.Screen.Y);
        if (area < 0)
            Fill(a, b, c, area, color);
    }

    private static float[] BuildScreenDoor()
    {
        int[] bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        var thresholds = new float[bayer.Length];
        for (var i = 0; i < bayer.Length; i++)
            thresholds[i] = (bayer[i] + 0.5f) / bayer.Length;
        return thresholds;
    }

    /// <summary>Twice the signed area of the triangle a, b, p.</summary>
    private static float Edge(Vector3 a, Vector3 b, float px, float py) =>
        (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);

    private void Fill(in Corner cornerA, in Corner cornerB, in Corner cornerC, float area, Vector3 color)
    {
        var a = cornerA.Screen;
        var b = cornerB.Screen;
        var c = cornerC.Screen;
        var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        var maxX = Math.Min(Width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        var maxY = Math.Min(Height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        if (minX > maxX || minY > maxY)
            return;

        // Barycentric weights of a, b and c at the first pixel centre, and how much they change per pixel step.
        // Dividing by the (negative) area makes them positive inside the triangle.
        var inverseArea = 1f / area;
        var startX = minX + 0.5f;
        var startY = minY + 0.5f;
        var rowA = Edge(b, c, startX, startY) * inverseArea;
        var rowB = Edge(c, a, startX, startY) * inverseArea;
        var rowC = Edge(a, b, startX, startY) * inverseArea;
        var stepXA = (b.Y - c.Y) * inverseArea;
        var stepXB = (c.Y - a.Y) * inverseArea;
        var stepXC = (a.Y - b.Y) * inverseArea;
        var stepYA = (c.X - b.X) * inverseArea;
        var stepYB = (a.X - c.X) * inverseArea;
        var stepYC = (b.X - a.X) * inverseArea;

        // Lit pixels: world position / w and 1 / w are linear on screen, so their ratio is perspective-correct.
        var worldA = cornerA.World * cornerA.InverseW;
        var worldB = cornerB.World * cornerB.InverseW;
        var worldC = cornerC.World * cornerC.InverseW;
        var flat = TrueColor ? Vector3.Clamp(color, Vector3.Zero, Vector3.One) : color; // AddLights clamps its own

        for (var y = minY; y <= maxY; y++)
        {
            var weightA = rowA;
            var weightB = rowB;
            var weightC = rowC;
            var index = y * Width + minX;
            var tiles = (y >> TileShift) * _tilesX;

            for (var x = minX; x <= maxX; x++, index++)
            {
                if (weightA >= 0 && weightB >= 0 && weightC >= 0)
                {
                    var depth = weightA * a.Z + weightB * b.Z + weightC * c.Z;
                    if (depth < _depth[index])
                    {
                        var lights = _lightMask == 0 ? 0 : _lightMask & _tileLights[tiles + (x >> TileShift)];
                        var pixel = lights == 0
                            ? flat
                            : AddLights(color, lights, (weightA * worldA + weightB * worldB + weightC * worldC)
                                               / (weightA * cornerA.InverseW + weightB * cornerB.InverseW
                                                  + weightC * cornerC.InverseW));
                        if (TrueColor)
                        {
                            if (_opacity >= 1f)
                            {
                                _depth[index] = depth;
                                _shaded[index] = pixel;
                            }
                            else
                            {
                                // The GPU's SRC_ALPHA, ONE_MINUS_SRC_ALPHA blend; depth stays, so farther opaque
                                // triangles drawn later still cover it.
                                _shaded[index] = pixel * _opacity + _shaded[index] * (1f - _opacity);
                            }
                        }
                        else if (_opacity >= 1f || ScreenDoor[((y & 3) << 2) | (x & 3)] < _opacity)
                        {
                            _depth[index] = depth;
                            Colors[index] = _palette.ToConsoleColor(pixel, x, y);
                        }
                    }
                }

                weightA += stepXA;
                weightB += stepXB;
                weightC += stepXC;
            }

            rowA += stepYA;
            rowB += stepYB;
            rowC += stepYC;
        }
    }

    /// <summary>A transformed vertex: screen position and depth, 1 / w, and (lit meshes only) world position.</summary>
    private struct Corner
    {
        public Vector3 Screen;
        public float InverseW;
        public Vector3 World;
    }
}
