using System;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using Doom.Components;
using Doom.Graphics;
using Doom.Maps;
using Doom.Play;

namespace Doom.Rendering.Meshes;

/// <summary>
/// The view drawn by the engine's 3D renderer (<c>ConsoleEngine.Rendering3D</c>'s CPU <see cref="Rasterizer"/>, the
/// one Kartster uses) instead of Doom's BSP columns, to compare the two (V while playing). The level is
/// <see cref="LevelMeshes"/> (textured walls and flats), things are <see cref="SpriteMeshes"/> turned to face the
/// view plane; only what <see cref="Visibility"/> finds is drawn. Light: each subsector and thing gets its sector's
/// brightness (Doom's light table right in front), and the lighting's fog darkens with distance, as Doom's tables
/// do. Sky ceilings come out in a key colour that is swapped for the sky picture. The 24-bit frame is turned back
/// into palette indices (<see cref="PaletteQuantizer"/>). Nothing is allocated per frame, except while a moving
/// sector's mesh grows and the first time a texture or sprite picture is seen.
/// </summary>
public sealed class MeshRenderer : IViewRenderer, IDisposable
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>World units per light table further away, and how many tables brighter things right in front are.</summary>
    private const float UnitsPerLightTable = 96;

    private const int NearBoost = 4;

    /// <summary>
    /// Where the fog is black: Doom's light fades one table (of 32) per 96 units; the fog fades the brightness right in
    /// front (4 tables up) to nothing over the same span.
    /// </summary>
    private const float FogEnd = (Palette.LightLevels + NearBoost) * UnitsPerLightTable;
    private const float FarPlane = 16384;

    /// <summary>Sky texture columns per full turn (as <see cref="WorldRenderer"/>).</summary>
    private const float SkyColumnsPerTurn = 1024;

    private readonly FrameBuffer _frame;
    private readonly Palette _palette;
    private readonly TextureLibrary _textures;
    private readonly Rasterizer _rasterizer;
    private readonly LevelMeshes _level;
    private readonly SpriteMeshes _sprites;
    private readonly PaletteQuantizer _quantizer;
    private readonly Visibility _visibility;
    private readonly SceneLighting _lighting = new() { SunColor = Vector3.Zero };
    private readonly uint[] _background;
    private readonly int[] _skyColumn;
    private readonly Matrix4x4 _projection;
    private Vector3 _forward;
    private Vector3 _right;
    private Matrix4x4 _viewProjection;
    private int _fixedLight;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <summary>Builds the meshes of <paramref name="map"/>: done once per level, it allocates.</summary>
    public MeshRenderer(FrameBuffer frame, Palette palette, TextureLibrary textures, Map map)
    {
        _frame = frame;
        _palette = palette;
        _textures = textures;
        _rasterizer = new Rasterizer(frame.Width, frame.Height, ConsolePalette.Campbell, trueColor: true);
        var textureCache = new TextureCache(palette, textures);
        _level = new LevelMeshes(map, textures, textureCache);
        _sprites = new SpriteMeshes(textureCache);
        _quantizer = new PaletteQuantizer(palette);
        _visibility = new Visibility(frame.Width, map.Sectors.Length, _level.Polygons);
        _background = new uint[frame.Width * frame.Height];
        _skyColumn = new int[frame.Width];

        // Square pixels, 90° across like WorldRenderer: focal length = half the width.
        var fieldOfViewY = 2 * MathF.Atan(frame.Height / (float)frame.Width);
        _projection = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfViewY, frame.Width / (float)frame.Height,
            WorldRenderer.NearPlane, FarPlane);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public FrameBuffer Frame => _frame;

    public LevelMeshes LevelMeshes => _level;

    /// <summary>Triangles handed to the rasterizer in the last frame (after culling whole meshes).</summary>
    public int TrianglesDrawn { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Render(Level level, in Camera camera, int skyTexture, int fixedLight = -1)
    {
        _fixedLight = fixedLight;
        _level.Update();
        SetView(camera);

        _rasterizer.Clear(_background);
        _lighting.FogEnd = fixedLight >= 0 ? 0 : FogEnd;
        _rasterizer.Lighting = _lighting;
        TrianglesDrawn = 0;
        _visibility.Find(level.Map, camera);
        var subsectors = level.Map.Subsectors;
        foreach (var i in _visibility.VisibleSubsectors)
        {
            var meshes = _level.Subsectors[i];
            var light = Brightness(subsectors[i].Sector.LightLevel);
            Draw(meshes.Walls, meshes.WallsMin, meshes.WallsMax, Matrix4x4.Identity, light);
            Draw(meshes.Flats, meshes.FlatsMin, meshes.FlatsMax, Matrix4x4.Identity, light);
            Draw(meshes.Sky, meshes.SkyMin, meshes.SkyMax, Matrix4x4.Identity, 1);
        }

        DrawThings(level, camera);
        _rasterizer.Finish();
        Present(camera, skyTexture);
    }

    public void Dispose() => _rasterizer.Dispose();

    //**********************************************************
    //** internals
    //**********************************************************

    private void SetView(in Camera camera)
    {
        var eye = MeshBuilder.ToWorld(camera.X, camera.Y, camera.Z);
        _forward = new Vector3(camera.Cos, 0, -camera.Sin);
        _right = new Vector3(camera.Sin, 0, camera.Cos);
        _viewProjection = Matrix4x4.CreateLookAt(eye, eye + _forward, Vector3.UnitY) * _projection;
    }

    /// <summary>Draws a mesh lit by <paramref name="light"/>, unless it is off screen.</summary>
    private void Draw(Mesh mesh, Vector3 min, Vector3 max, in Matrix4x4 world, float light)
    {
        if (mesh == null || !IsOnScreen(min, max, world))
            return;

        _lighting.Ambient = new Vector3(light);
        _rasterizer.Draw(mesh, world, _viewProjection, Vector3.UnitY);
        TrianglesDrawn += mesh.TriangleCount;
    }

    /// <summary>False when the box (in model space) is wholly outside one side of the view.</summary>
    private bool IsOnScreen(Vector3 min, Vector3 max, in Matrix4x4 world)
    {
        var transform = world * _viewProjection;
        int left = 0, right = 0, below = 0, above = 0, behind = 0;
        for (var corner = 0; corner < 8; corner++)
        {
            var point = new Vector3((corner & 1) == 0 ? min.X : max.X, (corner & 2) == 0 ? min.Y : max.Y,
                (corner & 4) == 0 ? min.Z : max.Z);
            var clip = Vector4.Transform(point, transform);
            left += clip.X < -clip.W ? 1 : 0;
            right += clip.X > clip.W ? 1 : 0;
            below += clip.Y < -clip.W ? 1 : 0;
            above += clip.Y > clip.W ? 1 : 0;
            behind += clip.Z < 0 ? 1 : 0;
        }

        return left < 8 && right < 8 && below < 8 && above < 8 && behind < 8;
    }

    private void DrawThings(Level level, in Camera camera)
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

                ref var body = ref bodies[i];
                if (!_visibility.IsSectorVisible(body.Sector))
                    continue;

                camera.ToView(body.X, body.Y, out _, out var depth);
                if (depth < WorldRenderer.NearPlane * 2)
                    continue;

                var frame = sprites.Get(animation.Sprite).Get(animation.Frame);
                if (frame == null)
                    continue;

                // Which of the eight rotations faces the viewer (as WorldRenderer.DrawSprite).
                var toViewer = MathF.Atan2(camera.Y - body.Y, camera.X - body.X);
                var rotation = (int)(Level.WrapAngle(toViewer - body.Angle + MathF.PI / 8) / (MathF.PI / 4)) & 7;
                var picture = frame.Get(rotation, out var mirrored);
                if (picture == null)
                    continue;

                var bright = (thing.Flags & ThingFlags.FullBright) != 0;
                var mesh = _sprites.Get(picture, mirrored, bright);
                var light = bright ? 1 : Brightness(sectors[body.Sector].LightLevel);
                var position = MeshBuilder.ToWorld(body.X, body.Y, body.Z);
                var world = new Matrix4x4(
                    _right.X, _right.Y, _right.Z, 0,
                    0, 1, 0, 0,
                    -_forward.X, -_forward.Y, -_forward.Z, 0,
                    position.X, position.Y, position.Z, 1);
                var reach = picture.Width + Math.Abs(picture.LeftOffset);
                Draw(mesh, new Vector3(-reach, picture.TopOffset - picture.Height, 0),
                    new Vector3(reach, picture.TopOffset, 0), world, light);
            }
        }
    }

    /// <summary>
    /// A sector light level (0..255) as a brightness for the rasterizer: Doom's light table right in front of the
    /// viewer. The fog takes it down with distance.
    /// </summary>
    private float Brightness(int lightLevel)
    {
        if (_fixedLight >= 0)
            return 1;

        var table = ((255 - Math.Clamp(lightLevel, 0, 255)) >> 3) - NearBoost;
        return 1 - Math.Clamp(table, 0, Palette.LightLevels - 1) / (float)Palette.LightLevels;
    }

    /// <summary>The rasterizer's frame into the palette frame: nearest palette colours, the sky, the fixed light.</summary>
    private void Present(in Camera camera, int skyTexture)
    {
        var width = _frame.Width;
        var height = _frame.Height;
        var sky = skyTexture >= 0 ? _textures.Textures[skyTexture] : null;
        if (sky != null)
        {
            for (var x = 0; x < width; x++)
            {
                var slope = (x + 0.5f - width / 2f) / (width / 2f);
                var angle = camera.Angle - MathF.Atan(slope);
                var column = (int)MathF.Floor(-angle / (MathF.PI * 2) * SkyColumnsPerTurn) % sky.Width;
                _skyColumn[x] = (column < 0 ? column + sky.Width : column) * sky.Height;
            }
        }

        var rgb = _rasterizer.Rgb;
        var pixels = _frame.Pixels;
        var colorMaps = _palette.ColorMaps;
        var lightOffset = _fixedLight * Palette.ColorCount;
        var skyRowScale = 200f / height;
        for (var y = 0; y < height; y++)
        {
            var skyRow = sky == null ? 0 : Math.Min((int)(y * skyRowScale), sky.Height - 1);
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                var color = rgb[i] != LevelMeshes.SkyKey ? _quantizer.Nearest(rgb[i])
                    : sky != null ? sky.Pixels[_skyColumn[x] + skyRow] : (byte)0;
                pixels[i] = _fixedLight >= 0 ? colorMaps[lightOffset + color] : color;
            }
        }
    }
}
