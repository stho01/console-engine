using System;
using Doom.Components;
using Doom.Graphics;
using Doom.Maps;
using Doom.Play;

namespace Doom.Rendering;

/// <summary>
/// Draws the 3D view into a <see cref="FrameBuffer"/>. The BSP tree is walked front to back; each visible seg draws
/// its wall columns, and the floor and ceiling between the column's clip window and the wall, then narrows the
/// window (a one-sided wall closes the column). Floors and ceilings are textured per pixel, the sky by view angle.
/// See-through middle textures and sprites are drawn last against the depth buffer. Lighting picks a
/// <c>COLORMAP</c> table from the sector light and the distance. Nothing is allocated per frame.
/// </summary>
public sealed class WorldRenderer : IViewRenderer
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Things closer than this (world units) are not drawn; walls are cut at it.</summary>
    public const float NearPlane = 4;

    /// <summary>Horizontal field of view: 90°, like Doom.</summary>
    private const float HalfFieldOfViewTan = 1f;

    /// <summary>Light tables per world unit of distance, and how many tables brighter things right in front are.</summary>
    private const float DistanceShade = 1 / 96f;

    private const int NearBoost = 4;

    /// <summary>Lines exactly along x or y get a little darker or lighter, so corners read (like Doom).</summary>
    private const int FakeContrast = 16;

    private const int MaskedSpanCapacity = 256;
    /// <summary>Clip rows kept per view column for see-through middle textures (two per column of a span).</summary>
    private const int MaskedClipsPerColumn = 200;

    /// <summary>Sky texture columns per full turn.</summary>
    private const float SkyColumnsPerTurn = 1024;

    private readonly FrameBuffer _frame;
    private readonly Palette _palette;
    private readonly TextureLibrary _textures;
    private readonly int _width;
    private readonly int _height;
    private readonly float _halfWidth;
    private readonly float _horizon;
    private readonly float _focal;
    private readonly float _skyRowScale;
    private readonly int[] _ceilingClip;
    private readonly int[] _floorClip;
    private readonly bool[] _solid;
    private readonly float[] _rayX;
    private readonly float[] _rayY;
    private readonly float[] _columnSlope;
    private readonly int[] _skyColumn;
    private readonly MaskedSpan[] _masked = new MaskedSpan[MaskedSpanCapacity];
    private readonly int[] _maskedClips;
    private int _maskedCount;
    private int _maskedClipCount;
    private int _openColumns;
    private Map _map;
    private Camera _camera;
    private int _skyTexture;
    private int _fixedLight;
    private int _tic;

    /// <summary>A see-through middle texture to draw after the walls (grates, bars, fences).</summary>
    private struct MaskedSpan
    {
        public int X1, X2, ClipStart, Texture, Light;
        public float ScreenX1, ScreenX2, InverseDepth1, InverseDepth2, UOverDepth1, UOverDepth2, Anchor, Bottom, Top;
        public float XOffset;
    }

    //**********************************************************
    //** ctor
    //**********************************************************

    public WorldRenderer(FrameBuffer frame, Palette palette, TextureLibrary textures)
    {
        _frame = frame;
        _palette = palette;
        _textures = textures;
        _width = frame.Width;
        _height = frame.Height;
        _halfWidth = _width / 2f;
        _horizon = _height / 2f;
        _focal = _halfWidth / HalfFieldOfViewTan;
        _skyRowScale = 200f / _height;
        _ceilingClip = new int[_width];
        _floorClip = new int[_width];
        _solid = new bool[_width];
        _rayX = new float[_width];
        _rayY = new float[_width];
        _columnSlope = new float[_width];
        _skyColumn = new int[_width];
        _maskedClips = new int[MaskedClipsPerColumn * _width];
        for (var x = 0; x < _width; x++)
            _columnSlope[x] = (x + 0.5f - _halfWidth) / _focal;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public FrameBuffer Frame => _frame;

    /// <summary>World units to screen pixels at distance 1.</summary>
    public float Focal => _focal;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Renders <paramref name="level"/> from <paramref name="camera"/>. <paramref name="fixedLight"/> is a light
    /// table to use everywhere (light amplification), or -1.
    /// </summary>
    public void Render(Level level, in Camera camera, int skyTexture, int fixedLight = -1)
    {
        _map = level.Map;
        _camera = camera;
        _skyTexture = skyTexture;
        _fixedLight = fixedLight;
        _tic = level.Tic;
        _frame.Clear(0);
        _maskedCount = 0;
        _maskedClipCount = 0;
        _openColumns = _width;
        Array.Fill(_ceilingClip, 0);
        Array.Fill(_floorClip, _height);
        Array.Fill(_solid, false);

        var skyWidth = skyTexture >= 0 ? _textures.Textures[skyTexture].Width : 1;
        for (var x = 0; x < _width; x++)
        {
            var slope = _columnSlope[x];
            _rayX[x] = camera.Cos + camera.Sin * slope;
            _rayY[x] = camera.Sin - camera.Cos * slope;
            var angle = camera.Angle - MathF.Atan(slope);
            _skyColumn[x] = PositiveModulo((int)MathF.Floor(-angle / (MathF.PI * 2) * SkyColumnsPerTurn), skyWidth);
        }

        RenderNode(_map.Root);
        DrawMaskedSpans();
        DrawSprites(level);
    }

    //**********************************************************
    //** internals: BSP walk and walls
    //**********************************************************

    private void RenderNode(int child)
    {
        if (_openColumns <= 0)
            return;

        if ((child & Map.SubsectorFlag) != 0)
        {
            var subsector = _map.Subsectors[child & ~Map.SubsectorFlag];
            var end = subsector.FirstSeg + subsector.SegCount;
            for (var i = subsector.FirstSeg; i < end && i < _map.Segs.Length; i++)
                DrawSeg(_map.Segs[i]);
            return;
        }

        var node = _map.Nodes[child];
        var behind = node.IsOnBackSide(_camera.X, _camera.Y);
        RenderNode(behind ? node.Back : node.Front);
        RenderNode(behind ? node.Front : node.Back);
    }

    private void DrawSeg(Seg seg)
    {
        var side = seg.Side;
        var front = seg.FrontSector;
        if (side == null || front == null)
            return;

        var v1 = seg.V1;
        var v2 = seg.V2;
        if ((v2.X - v1.X) * (_camera.Y - v1.Y) - (v2.Y - v1.Y) * (_camera.X - v1.X) >= 0)
            return;

        _camera.ToView(v1.X, v1.Y, out var side1, out var depth1);
        _camera.ToView(v2.X, v2.Y, out var side2, out var depth2);
        var u1 = seg.Offset;
        var u2 = seg.Offset + seg.Length;
        if (depth1 < NearPlane && depth2 < NearPlane)
            return;

        if (depth1 < NearPlane)
        {
            var t = (NearPlane - depth1) / (depth2 - depth1);
            side1 += (side2 - side1) * t;
            u1 += (u2 - u1) * t;
            depth1 = NearPlane;
        }
        else if (depth2 < NearPlane)
        {
            var t = (NearPlane - depth2) / (depth1 - depth2);
            side2 += (side1 - side2) * t;
            u2 += (u1 - u2) * t;
            depth2 = NearPlane;
        }

        var screenX1 = _halfWidth + side1 * _focal / depth1;
        var screenX2 = _halfWidth + side2 * _focal / depth2;
        if (screenX2 <= screenX1)
            return;

        var x1 = Math.Max(0, RowAt(screenX1));
        var x2 = Math.Min(_width, RowAt(screenX2));
        if (x1 >= x2)
            return;

        var line = seg.Line;
        line.Mapped = true;
        var back = seg.BackSector;
        var eye = _camera.Z;
        var light = front.LightLevel;
        if (v1.Y == v2.Y)
            light -= FakeContrast;
        else if (v1.X == v2.X)
            light += FakeContrast;

        var sky = _textures.SkyFlat;
        var ceilingIsSky = front.CeilingFlat == sky && sky >= 0;
        var floorIsSky = front.FloorFlat == sky && sky >= 0;
        var drawCeiling = front.CeilingHeight > eye || ceilingIsSky;
        var drawFloor = front.FloorHeight < eye;
        var lowerUnpegged = (line.Flags & LineFlags.LowerUnpegged) != 0;
        var upperUnpegged = (line.Flags & LineFlags.UpperUnpegged) != 0;

        var middle = AnimatedTexture(side.Middle);
        var upper = AnimatedTexture(side.Upper);
        var lower = AnimatedTexture(side.Lower);
        float middleAnchor = 0, upperAnchor = 0, lowerAnchor = 0;
        if (back == null)
        {
            if (middle >= 0)
                middleAnchor = (lowerUnpegged ? front.FloorHeight + _textures.Textures[middle].Height
                    : front.CeilingHeight) + side.YOffset;
        }
        else
        {
            if (upper >= 0)
                upperAnchor = (upperUnpegged ? front.CeilingHeight
                    : back.CeilingHeight + _textures.Textures[upper].Height) + side.YOffset;
            lowerAnchor = (lowerUnpegged ? front.CeilingHeight : back.FloorHeight) + side.YOffset;
        }

        var skyHack = ceilingIsSky && back != null && back.CeilingFlat == sky;
        var inverse1 = 1 / depth1;
        var inverse2 = 1 / depth2;
        var uOver1 = u1 * inverse1;
        var uOver2 = u2 * inverse2;
        var span = screenX2 - screenX1;
        var maskedClipStart = -1;
        var recordMasked = back != null && middle >= 0 && _maskedCount < MaskedSpanCapacity
                           && _maskedClipCount + 2 * (x2 - x1) <= _maskedClips.Length;
        if (recordMasked)
        {
            maskedClipStart = _maskedClipCount;
            _maskedClipCount += 2 * (x2 - x1);
        }

        for (var x = x1; x < x2; x++)
        {
            var clipIndex = maskedClipStart + 2 * (x - x1);
            if (_solid[x])
            {
                if (recordMasked)
                {
                    _maskedClips[clipIndex] = 0;
                    _maskedClips[clipIndex + 1] = 0;
                }

                continue;
            }

            var t = (x + 0.5f - screenX1) / span;
            var inverse = inverse1 + (inverse2 - inverse1) * t;
            var depth = 1 / inverse;
            var u = (uOver1 + (uOver2 - uOver1) * t) * depth + side.XOffset;
            var scale = _focal * inverse;
            var top = _ceilingClip[x];
            var bottom = _floorClip[x];
            var ceilingRow = Math.Clamp(RowAt(_horizon - (front.CeilingHeight - eye) * scale), top, bottom);
            var floorRow = Math.Clamp(RowAt(_horizon - (front.FloorHeight - eye) * scale), ceilingRow, bottom);
            if (drawCeiling)
                DrawPlane(x, top, ceilingRow, front.CeilingHeight, front.CeilingFlat, front.LightLevel, ceilingIsSky);
            if (drawFloor)
                DrawPlane(x, floorRow, bottom, front.FloorHeight, front.FloorFlat, front.LightLevel, floorIsSky);

            var lightIndex = LightIndex(light, depth);
            if (back == null)
            {
                DrawWallColumn(x, ceilingRow, floorRow, middle, middleAnchor, depth, u, lightIndex);
                MarkSolid(x);
                continue;
            }

            var newTop = ceilingRow;
            if (back.CeilingHeight < front.CeilingHeight && !skyHack)
            {
                var backCeilingRow = Math.Clamp(RowAt(_horizon - (back.CeilingHeight - eye) * scale), ceilingRow,
                    floorRow);
                DrawWallColumn(x, ceilingRow, backCeilingRow, upper, upperAnchor, depth, u, lightIndex);
                newTop = backCeilingRow;
            }

            var newBottom = floorRow;
            if (back.FloorHeight > front.FloorHeight)
            {
                var backFloorRow = Math.Clamp(RowAt(_horizon - (back.FloorHeight - eye) * scale), newTop, floorRow);
                DrawWallColumn(x, backFloorRow, floorRow, lower, lowerAnchor, depth, u, lightIndex);
                newBottom = backFloorRow;
            }

            if (recordMasked)
            {
                _maskedClips[clipIndex] = newTop;
                _maskedClips[clipIndex + 1] = newBottom;
            }

            _ceilingClip[x] = newTop;
            _floorClip[x] = newBottom;
            if (newTop >= newBottom || back.CeilingHeight <= back.FloorHeight)
                MarkSolid(x);
        }

        if (!recordMasked)
            return;

        var texture = _textures.Textures[middle];
        var openBottom = MathF.Max(front.FloorHeight, back.FloorHeight);
        var openTop = MathF.Min(front.CeilingHeight, back.CeilingHeight);
        var anchor = (lowerUnpegged ? openBottom + texture.Height : openTop) + side.YOffset;
        _masked[_maskedCount++] = new MaskedSpan
        {
            X1 = x1, X2 = x2, ClipStart = maskedClipStart, Texture = middle, Light = light,
            ScreenX1 = screenX1, ScreenX2 = screenX2, InverseDepth1 = inverse1, InverseDepth2 = inverse2,
            UOverDepth1 = uOver1, UOverDepth2 = uOver2, Anchor = anchor, Bottom = openBottom, Top = openTop,
            XOffset = side.XOffset
        };
    }

    private void MarkSolid(int x)
    {
        if (_solid[x])
            return;

        _solid[x] = true;
        _ceilingClip[x] = _height;
        _floorClip[x] = 0;
        _openColumns--;
    }

    /// <summary>The texture an index shows this tic (animation), or -1.</summary>
    private int AnimatedTexture(int texture) =>
        texture >= 0 && texture < _textures.TextureFrame.Length ? _textures.TextureFrame[texture] : -1;

    private void DrawWallColumn(int x, int y0, int y1, int texture, float anchor, float depth, float u, int light)
    {
        if (y0 >= y1)
            return;

        var pixels = _frame.Pixels;
        var depths = _frame.Depth;
        if (texture < 0)
        {
            for (var y = y0; y < y1; y++)
                depths[y * _width + x] = depth;
            return;
        }

        var tex = _textures.Textures[texture];
        var column = PositiveModulo((int)MathF.Floor(u), tex.Width) * tex.Height;
        var step = depth / _focal;
        var v = anchor - (_camera.Z + (_horizon - (y0 + 0.5f)) * step);
        var colorMaps = _palette.ColorMaps;
        var lightOffset = light * Palette.ColorCount;
        for (var y = y0; y < y1; y++)
        {
            var row = PositiveModulo((int)MathF.Floor(v), tex.Height);
            var index = y * _width + x;
            pixels[index] = colorMaps[lightOffset + tex.Pixels[column + row]];
            depths[index] = depth;
            v += step;
        }
    }

    /// <summary>Floor or ceiling pixels of column <paramref name="x"/> in rows [y0, y1).</summary>
    private void DrawPlane(int x, int y0, int y1, float height, int flat, int light, bool sky)
    {
        if (y0 >= y1)
            return;

        var pixels = _frame.Pixels;
        var depths = _frame.Depth;
        var colorMaps = _palette.ColorMaps;
        if (sky)
        {
            if (_skyTexture < 0)
                return;

            var skyTexture = _textures.Textures[_skyTexture];
            var column = _skyColumn[x] * skyTexture.Height;
            var skyLight = (_fixedLight >= 0 ? _fixedLight : 0) * Palette.ColorCount;
            for (var y = y0; y < y1; y++)
            {
                var row = Math.Clamp((int)(y * _skyRowScale), 0, skyTexture.Height - 1);
                pixels[y * _width + x] = colorMaps[skyLight + skyTexture.Pixels[column + row]];
            }

            return;
        }

        var frame = flat >= 0 && flat < _textures.FlatFrame.Length ? _textures.FlatFrame[flat] : -1;
        if (frame < 0)
            return;

        var flatPixels = _textures.Flats[frame].Pixels;
        var heightAboveEye = _camera.Z - height;
        var rayX = _rayX[x];
        var rayY = _rayY[x];
        for (var y = y0; y < y1; y++)
        {
            var dy = y + 0.5f - _horizon;
            if (MathF.Abs(dy) < 0.01f)
                continue;

            var depth = heightAboveEye * _focal / dy;
            if (depth <= 0)
                continue;

            var worldX = _camera.X + rayX * depth;
            var worldY = _camera.Y + rayY * depth;
            var texel = flatPixels[(((int)MathF.Floor(-worldY)) & Flat.Mask) * Flat.Size
                                   + (((int)MathF.Floor(worldX)) & Flat.Mask)];
            var index = y * _width + x;
            pixels[index] = colorMaps[LightIndex(light, depth) * Palette.ColorCount + texel];
            depths[index] = depth;
        }
    }

    /// <summary>See-through middle textures: drawn once (no vertical tiling) inside the opening, depth tested.</summary>
    private void DrawMaskedSpans()
    {
        var pixels = _frame.Pixels;
        var depths = _frame.Depth;
        var colorMaps = _palette.ColorMaps;
        for (var i = 0; i < _maskedCount; i++)
        {
            ref var span = ref _masked[i];
            var texture = _textures.Textures[span.Texture];
            var width = span.ScreenX2 - span.ScreenX1;
            for (var x = span.X1; x < span.X2; x++)
            {
                var clipIndex = span.ClipStart + 2 * (x - span.X1);
                var top = _maskedClips[clipIndex];
                var bottom = _maskedClips[clipIndex + 1];
                if (top >= bottom)
                    continue;

                var t = (x + 0.5f - span.ScreenX1) / width;
                var inverse = span.InverseDepth1 + (span.InverseDepth2 - span.InverseDepth1) * t;
                var depth = 1 / inverse;
                var u = (span.UOverDepth1 + (span.UOverDepth2 - span.UOverDepth1) * t) * depth + span.XOffset;
                var column = PositiveModulo((int)MathF.Floor(u), texture.Width) * texture.Height;
                var step = depth / _focal;
                var lightOffset = LightIndex(span.Light, depth) * Palette.ColorCount;
                for (var y = top; y < bottom; y++)
                {
                    var worldZ = _camera.Z + (_horizon - (y + 0.5f)) * step;
                    if (worldZ < span.Bottom || worldZ > span.Top)
                        continue;

                    var row = (int)MathF.Floor(span.Anchor - worldZ);
                    if (row < 0 || row >= texture.Height || !texture.Opaque[column + row])
                        continue;

                    var index = y * _width + x;
                    if (depth >= depths[index])
                        continue;

                    pixels[index] = colorMaps[lightOffset + texture.Pixels[column + row]];
                    depths[index] = depth;
                }
            }
        }
    }

    //**********************************************************
    //** internals: sprites
    //**********************************************************

    private void DrawSprites(Level level)
    {
        var sprites = level.Sprites;
        var sectors = level.Map.Sectors;
        foreach (var chunk in level.Things.GetChunks())
        {
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            var animations = chunk.GetSpan<Animation>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var thing = ref things[i];
                ref var animation = ref animations[i];
                if (thing.Removed || animation.Sprite < 0 || (thing.Flags & ThingFlags.Invisible) != 0)
                    continue;

                DrawSprite(sprites.Get(animation.Sprite), animation.Frame, thing.Flags, bodies[i],
                    sectors[bodies[i].Sector].LightLevel);
            }
        }
    }

    private void DrawSprite(SpriteDefinition sprite, char letter, ThingFlags flags, in Body body, int sectorLight)
    {
        _camera.ToView(body.X, body.Y, out var side, out var depth);
        if (depth < NearPlane * 2)
            return;

        var frame = sprite.Get(letter);
        if (frame == null)
            return;

        var toViewer = MathF.Atan2(_camera.Y - body.Y, _camera.X - body.X);
        var relative = Level.WrapAngle(toViewer - body.Angle + MathF.PI / 8);
        var rotation = (int)(relative / (MathF.PI / 4)) & 7;
        var picture = frame.Get(rotation, out var mirrored);
        if (picture == null)
            return;

        var scale = _focal / depth;
        var centerX = _halfWidth + side * scale;
        var left = centerX - (mirrored ? picture.Width - picture.LeftOffset : picture.LeftOffset) * scale;
        var top = _horizon - (body.Z + picture.TopOffset - _camera.Z) * scale;
        var x0 = Math.Max(0, RowAt(left));
        var x1 = Math.Min(_width, RowAt(left + picture.Width * scale));
        var y0 = Math.Max(0, RowAt(top));
        var y1 = Math.Min(_height, RowAt(top + picture.Height * scale));
        if (x0 >= x1 || y0 >= y1)
            return;

        var bright = (flags & ThingFlags.FullBright) != 0;
        var light = _fixedLight >= 0 ? _fixedLight : bright ? 0 : LightIndex(sectorLight, depth);
        var shadow = (flags & ThingFlags.Shadow) != 0;
        if (shadow)
            light = Math.Min(Palette.LightLevels - 1, light + 6);

        var lightOffset = light * Palette.ColorCount;
        var pixels = _frame.Pixels;
        var depths = _frame.Depth;
        var colorMaps = _palette.ColorMaps;
        for (var x = x0; x < x1; x++)
        {
            var column = (int)((x + 0.5f - left) / scale);
            column = Math.Clamp(mirrored ? picture.Width - 1 - column : column, 0, picture.Width - 1);
            var columnStart = column * picture.Height;
            for (var y = y0; y < y1; y++)
            {
                if (shadow && ((x + y + _tic) & 1) == 0)
                    continue;

                var row = Math.Clamp((int)((y + 0.5f - top) / scale), 0, picture.Height - 1);
                if (!picture.Opaque[columnStart + row])
                    continue;

                var index = y * _width + x;
                if (depth >= depths[index])
                    continue;

                pixels[index] = colorMaps[lightOffset + picture.Pixels[columnStart + row]];
                depths[index] = depth;
            }
        }
    }

    //**********************************************************
    //** internals: helpers
    //**********************************************************

    /// <summary>The first pixel row (or column) whose centre is at or past <paramref name="position"/>.</summary>
    private static int RowAt(float position) => (int)MathF.Ceiling(position - 0.5f);

    private int LightIndex(int light, float depth)
    {
        if (_fixedLight >= 0)
            return _fixedLight;

        var baseIndex = (255 - Math.Clamp(light, 0, 255)) >> 3;
        var index = baseIndex + (int)(depth * DistanceShade) - NearBoost;
        return Math.Clamp(index, 0, Palette.LightLevels - 1);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
